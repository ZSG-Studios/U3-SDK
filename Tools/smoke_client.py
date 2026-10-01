"""Run the development Windows client without taking focus; bound startup/gameplay to 240 seconds."""
import json
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import time
import threading

PROJECT = Path(__file__).resolve().parents[1]
BUILD = PROJECT / "Builds" / "Windows64"
LOGS = PROJECT / "Logs"
UNITY = shutil.which("unity")


def command(name, *args, timeout=8):
    completed = subprocess.run(
        [UNITY, "command", "--runtime-path", str(BUILD), "--timeout", str(timeout), "--result-only", name, *args],
        cwd=PROJECT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout+4,
    )
    if completed.returncode:
        try:
            response = json.loads(completed.stdout)
            details = "; ".join(e.get("message", "") for e in response.get("errors", []))
        except json.JSONDecodeError:
            details = "The CLI returned no structured error"
        (LOGS / ("standalone-command-failure-" + name + ".json")).write_text(completed.stdout, encoding="utf-8")
        raise RuntimeError(f"Runtime command {name} failed (exit {completed.returncode}): {details[:2000]}")
    return json.loads(completed.stdout)


def evaluate(code, timeout=8):
    # The separator sends the millisecond timeout to the tool, independently of the CLI's seconds timeout.
    result = command("eval", code, "--", "--timeout", str(timeout * 1000), timeout=timeout)
    if not result.get("success"):
        raise RuntimeError(f"Runtime evaluation failed: {result.get('diagnostics')}")
    return result.get("result")


def state():
    return evaluate('UnityEngine.QualitySettings.vSyncCount = 0; '
                    'UnityEngine.Application.targetFrameRate = 60; UnityEngine.Application.runInBackground = true; '
                    'return new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, '
                    'assetsLoading = SDG.Unturned.Assets.isLoading, loadingBlocked = SDG.Unturned.LoadingUI.isBlocked, '
                    'levelLoaded = SDG.Unturned.Level.isLoaded, playerPresent = SDG.Unturned.Player.LocalPlayer != null, '
                    'timeScale = UnityEngine.Time.timeScale, deltaTime = UnityEngine.Time.deltaTime, '
                    'frame = UnityEngine.Time.frameCount, targetFrameRate = UnityEngine.Application.targetFrameRate, '
                    'realtime = UnityEngine.Time.realtimeSinceStartupAsDouble, '
                    'pipeline = UnityEngine.Rendering.RenderPipelineManager.currentPipeline?.GetType().Name, '
                    'gpu = UnityEngine.SystemInfo.graphicsDeviceName, '
                    'graphicsApi = UnityEngine.SystemInfo.graphicsDeviceType.ToString() };')


def capture(name):
    path = LOGS / name
    # JSON string escaping is valid for this generated C# string literal; no shell is involved.
    # An occluded Windows player may stop presenting entirely. Render the world camera
    # explicitly; this image verifies scene rendering, but excludes overlay HUD canvases.
    evaluate('var camera = UnityEngine.Camera.main; '
             'if (camera == null) throw new System.InvalidOperationException("No main camera"); '
             'var previousTarget = camera.targetTexture; var previousActive = UnityEngine.RenderTexture.active; '
             'var target = UnityEngine.RenderTexture.GetTemporary(1280, 720, 24); '
             'var pixels = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false); '
             'try { camera.targetTexture = target; '
             'var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target }; '
             'UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request); UnityEngine.RenderTexture.active = target; '
             'pixels.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); '
             f'System.IO.File.WriteAllBytes({json.dumps(str(path))}, UnityEngine.ImageConversion.EncodeToPNG(pixels)); '
             '} finally { camera.targetTexture = previousTarget; UnityEngine.RenderTexture.active = previousActive; '
             'UnityEngine.RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(pixels); } return true;', timeout=40)
    return path


def fixture(filename, entry, timeout=40):
    """Compile a qualification fixture through Pipeline's own runtime compiler."""
    source = str(PROJECT / "Tools" / filename)
    code = ('var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); '
            'var owner = assemblies.First(a => a.GetType("Unity.Pipeline.Compilation.RoslynCompilationService") != null); '
            'var service = owner.GetType("Unity.Pipeline.Compilation.RoslynCompilationService"); '
            'var requestType = owner.GetType("Unity.Pipeline.Compilation.CompilationRequest"); '
            'var request = System.Activator.CreateInstance(requestType); '
            f'requestType.GetProperty("SourceCode").SetValue(request, System.IO.File.ReadAllText({json.dumps(source)})); '
            'requestType.GetProperty("AssemblyName").SetValue(request, "Qualification_" + System.Guid.NewGuid().ToString("N")); '
            'requestType.GetProperty("AdditionalAssemblyPrefixes").SetValue(request, new [] { "UnityEngine", "Unity.RenderPipelines", "UnityEx", "Assembly-CSharp", "AstarPathfindingProject", "Newtonsoft" }); '
            'var result = service.GetMethod("Compile", new [] {requestType}).Invoke(null, new [] {request}); '
            'if (!(bool)result.GetType().GetProperty("Success").GetValue(result)) '
            'throw new System.InvalidOperationException(string.Join("; ", ((System.Collections.IEnumerable)result.GetType().GetProperty("Diagnostics").GetValue(result)).Cast<object>().Select(d => string.Join(", ", d.GetType().GetProperties().Where(p => p.CanRead).Select(p => p.Name + "=" + p.GetValue(d)))))); '
            'var assembly = (System.Reflection.Assembly)result.GetType().GetProperty("Assembly").GetValue(result); '
            f'try {{ return assembly.GetType({json.dumps(entry)}).GetMethod("Main").Invoke(null, null); }} '
            'catch (System.Reflection.TargetInvocationException error) { throw new System.InvalidOperationException(error.GetBaseException().ToString()); }')
    return evaluate(code, timeout)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", default="Tutorial", choices=("Tutorial", "PEI", "Russia", "Germany", "Washington", "Yukon", "Alpha Valley", "Destruction", "Monolith", "Paintball_Arena_0"))
    parser.add_argument("--graphics-api", default="dx12", choices=("dx12", "vulkan"))
    parser.add_argument("--verify-graphics", action="store_true", help="Exercise graphics/display menu callbacks and rendering")
    options = parser.parse_args()
    suffix = "-" + options.graphics_api + ("" if options.map == "Tutorial" else "-" + options.map.lower())
    if not UNITY or not (BUILD / "Unturned.exe").is_file():
        raise RuntimeError("Build the Windows development player and install Unity CLI first")
    LOGS.mkdir(exist_ok=True)
    startup = subprocess.STARTUPINFO() if os.name == "nt" else None
    if startup:
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
    started = time.monotonic()
    process = subprocess.Popen(
        [str(BUILD / "Unturned.exe"), "-force-d3d12" if options.graphics_api == "dx12" else "-force-vulkan",
         "-NoWorkshopSubscriptions", "-FrameRateLimit", "60", "-screen-fullscreen", "0",
         "-screen-width", "1280", "-screen-height", "720", "-logFile", str(LOGS / ("standalone-client" + suffix + ".log"))],
        cwd=BUILD, startupinfo=startup, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    watchdog_fired = threading.Event()
    def stop_at_deadline():
        if process.poll() is None:
            watchdog_fired.set()
            try:
                process.kill()
            except ProcessLookupError:
                pass
    watchdog = threading.Timer(240, stop_at_deadline)
    watchdog.daemon = True
    watchdog.start()
    report = {"pid": process.pid, "map": options.map, "graphicsApi": options.graphics_api,
              "menu": None, "gameplay": None, "result": "failed"}
    try:
        phase = "menu"
        last_frame = None
        settled_at = None
        update_limit_installed = False
        while time.monotonic() - started < 240:
            if process.poll() is not None:
                raise RuntimeError(f"Client exited early: {process.returncode}; inspect Logs/standalone-client.log")
            try:
                current = state()
            except (RuntimeError, subprocess.TimeoutExpired, json.JSONDecodeError):
                time.sleep(3)
                continue
            if not update_limit_installed:
                # Occluded Windows players can bypass the presentation-based FPS wait.
                # Bound updates as soon as the server responds, including asset startup.
                evaluate('var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop(); '
                         'var systems = new UnityEngine.LowLevel.PlayerLoopSystem[loop.subSystemList.Length + 1]; '
                         'System.Array.Copy(loop.subSystemList, systems, loop.subSystemList.Length); '
                         'systems[systems.Length - 1] = new UnityEngine.LowLevel.PlayerLoopSystem { '
                         'type = typeof(System.Threading.Thread), updateDelegate = () => System.Threading.Thread.Sleep(17) }; '
                         'loop.subSystemList = systems; UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop); return true;')
                update_limit_installed = True
            ready = not current["assetsLoading"] and not current["loadingBlocked"]
            if phase == "menu" and current["scene"] == "Menu" and ready:
                report["menu"] = current
                # An occluded Windows player may not have rendered a frame yet.
                # A native render request initializes its configured pipeline.
                capture("standalone-menu" + suffix + ".png")
                current = state()
                report["menu"] = current
                if current["pipeline"] != "UniversalRenderPipeline":
                    raise RuntimeError("The player did not initialize native URP")
                expected_api = "Direct3D12" if options.graphics_api == "dx12" else "Vulkan"
                if current["graphicsApi"] != expected_api:
                    raise RuntimeError(f"Requested {expected_api}, but player initialized {current['graphicsApi']}")
                print(json.dumps({"phase": "menu", "state": current}), flush=True)
                mode = "TUTORIAL" if options.map == "Tutorial" else "NORMAL"
                evaluate(f'SDG.Unturned.OptionsSettings.pauseWhenUnfocused = false; UnityEngine.Time.timeScale = 1; SDG.Unturned.Provider.map = {json.dumps(options.map)}; '
                         f'SDG.Unturned.Provider.singleplayer(SDG.Unturned.EGameMode.{mode}, false); return true;')
                phase = "gameplay"
            elif phase == "gameplay" and current["levelLoaded"] and current["playerPresent"] and ready:
                if settled_at is None:
                    settled_at = time.monotonic()
                    last_frame = current["frame"]
                    last_realtime = current["realtime"]
                elif time.monotonic() - settled_at >= 10 and current["frame"] > last_frame:
                    report["gameplay"] = current
                    if current["timeScale"] <= 0 or current["deltaTime"] <= 0:
                        raise RuntimeError("Hidden gameplay remained paused; simulation was not qualified")
                    report["unsupportedMaterials"] = evaluate('return UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>()'
                        '.Where(r => r.enabled && r.gameObject.activeInHierarchy).SelectMany(r => r.sharedMaterials)'
                        '.Where(m => m != null && (!m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader"))'
                        '.GroupBy(m => m.shader.name).Select(g => new { shader = g.Key, count = g.Count(), '
                        'names = g.Select(m => m.name).Distinct().Take(4).ToArray() }).ToArray();')
                    report["legacyMaterials"] = evaluate('return UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>()'
                        '.Where(r => r.enabled && r.gameObject.activeInHierarchy).SelectMany(r => r.sharedMaterials)'
                        '.Where(m => m != null && m.GetTag("RenderPipeline", false) != "UniversalPipeline" '
                        '&& !m.shader.name.StartsWith("TextMeshPro/") && !m.shader.name.StartsWith("UI/"))'
                        '.GroupBy(m => m.shader.name).Select(g => new { shader = g.Key, count = g.Count(), '
                        'names = g.Select(m => m.name).Distinct().Take(4).ToArray() }).ToArray();')
                    report["settledUpdateRate"] = round((current["frame"] - last_frame) / (current["realtime"] - last_realtime), 2)
                    if report["settledUpdateRate"] > 65:
                        raise RuntimeError("The hidden client exceeded its test update limit")
                    capture("standalone-gameplay" + suffix + ".png")
                    if report["unsupportedMaterials"] or report["legacyMaterials"]:
                        report["materialOwners"] = evaluate('return UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMaterials.Any(m => m != null && m.GetTag("RenderPipeline",false) != "UniversalPipeline" && !m.shader.name.StartsWith("TextMeshPro/") && !m.shader.name.StartsWith("UI/"))).Select(r => new {path=string.Join("/",r.GetComponentsInParent<UnityEngine.Transform>().Reverse().Select(t=>t.name)),materials=r.sharedMaterials.Where(m=>m!=null).Select(m=>new {name=m.name,shader=m.shader.name}).ToArray()}).ToArray();')
                        raise RuntimeError("Gameplay contains unsupported material shaders: " + str(report["unsupportedMaterials"] + report["legacyMaterials"]))
                    if options.verify_graphics:
                        report["graphicsSettingsFixture"] = fixture("VerifyGraphicsSettings.cs", "VerifyGraphicsSettings", timeout=90)
                        if not report["graphicsSettingsFixture"].get("passed"):
                            raise RuntimeError("Graphics/display settings fixture failed")
                        display = evaluate('return new { width=UnityEngine.Screen.width, height=UnityEngine.Screen.height, '
                                           'mode=SDG.Unturned.GraphicsSettings.fullscreenMode.ToString(), '
                                           'scopeQuality=SDG.Unturned.GraphicsSettings.scopeQuality.ToString(), '
                                           'resolution=SDG.Unturned.GraphicsSettings.resolution };')
                        try:
                            evaluate('SDG.Unturned.GraphicsSettings.fullscreenMode=UnityEngine.FullScreenMode.Windowed; '
                                     'SDG.Unturned.GraphicsSettings.scopeQuality=SDG.Unturned.EGraphicQuality.OFF; '
                                     'SDG.Unturned.GraphicsSettings.resolution=new SDG.Unturned.GraphicsSettingsResolution {Width=1024,Height=768}; '
                                     'SDG.Unturned.GraphicsSettings.apply("qualify menu resolution change"); return true;')
                            time.sleep(3)
                            report["displayResolutionFixture"] = evaluate('return new { width=UnityEngine.Screen.width, '
                                'height=UnityEngine.Screen.height, mode=UnityEngine.Screen.fullScreenMode.ToString() };')
                            if report["displayResolutionFixture"] != {"width":1024,"height":768,"mode":"Windowed"}:
                                raise RuntimeError("Native resolution/fullscreen did not follow menu settings")
                            report["displayAspectRatio"] = evaluate('return SDG.Unturned.ScreenEx.GetCurrentAspectRatio();')
                            if abs(report["displayAspectRatio"] - 4/3) > .001:
                                raise RuntimeError("Windowed aspect ratio still reports monitor dimensions")
                            report["resizedScopeWidth"] = evaluate('var scope=(UnityEngine.RenderTexture)typeof(SDG.Unturned.PlayerLook)'
                                '.GetField("scopeRenderTexture",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)'
                                '.GetValue(SDG.Unturned.Player.LocalPlayer.look); return scope.width;')
                            if report["resizedScopeWidth"] != 768:
                                raise RuntimeError("Single-render scope did not resize after the window changed")
                        finally:
                            resolution = display["resolution"]
                            evaluate(f'SDG.Unturned.GraphicsSettings.fullscreenMode=UnityEngine.FullScreenMode.{display["mode"]}; '
                                     f'SDG.Unturned.GraphicsSettings.scopeQuality=SDG.Unturned.EGraphicQuality.{display["scopeQuality"]}; '
                                     'SDG.Unturned.GraphicsSettings.resolution=new SDG.Unturned.GraphicsSettingsResolution '
                                     f'{{Width={display["width"]},Height={display["height"]},RefreshRateNumerator={resolution["RefreshRateNumerator"]}u,'
                                     f'RefreshRateDenominator={resolution["RefreshRateDenominator"]}u}}; '
                                     'SDG.Unturned.GraphicsSettings.apply("restore display qualification settings"); return true;')
                            time.sleep(3)
                    if options.map == "Germany":
                        report["mirroredColliderFixture"] = fixture("VerifyMirroredColliders.cs", "VerifyMirroredColliders")
                        if not report["mirroredColliderFixture"].get("passed"):
                            raise RuntimeError("Mirrored collider geometry fixture failed")
                        report["waterFixture"] = fixture("VerifyURPWater.cs", "VerifyURPWater")
                        if not report["waterFixture"].get("passed"):
                            raise RuntimeError("Water/atmosphere fixture failed")
                        shutil.copyfile(report["waterFixture"]["screenshot"], LOGS / ("water-with-atmosphere" + suffix + ".png"))
                        navigation = fixture("VerifyRuntimeNavigation.cs", "VerifyRuntimeNavigation")
                        deadline = time.monotonic() + 25
                        navigation_path = Path(navigation["reportPath"])
                        while not navigation_path.is_file() and time.monotonic() < deadline:
                            time.sleep(1)
                        if not navigation_path.is_file():
                            raise TimeoutError("Native navigation fixture exceeded its watchdog")
                        report["navigationFixture"] = json.loads(navigation_path.read_text(encoding="utf-8"))
                        if not report["navigationFixture"].get("passed"):
                            raise RuntimeError("Native navigation fixture failed: " + str(report["navigationFixture"]))
                    time.sleep(3)
                    report["console"] = command("console_status")
                    if report["console"]["counts"]["error"]:
                        raise RuntimeError("Runtime console contains errors; inspect Logs/standalone-client.log")
                    if report["console"]["counts"]["warn"]:
                        raise RuntimeError("Runtime console contains warnings; inspect its persistent diagnostics")
                    # Some native GPU/resource errors bypass the managed console counter.
                    native_log = (LOGS / ("standalone-client" + suffix + ".log")).read_text(encoding="utf-8", errors="replace")
                    report["nativeRenderingErrors"] = [line for line in native_log.splitlines()
                        if "ERROR: Shader " in line or "Shader error in " in line
                        or "Releasing render texture that is set to be RenderTexture.active" in line]
                    if report["nativeRenderingErrors"]:
                        raise RuntimeError("Native rendering validation failed: " + str(report["nativeRenderingErrors"][:6]))
                    report["result"] = "passed"
                    print(json.dumps({"phase": "gameplay", "state": current}), flush=True)
                    break
            time.sleep(3)
        if report["result"] != "passed":
            raise TimeoutError("Startup/gameplay did not complete within the 240-second watchdog")
    except Exception as error:
        report["failure"] = str(error)
        try:
            report["console"] = command("console_status")
        except Exception:
            pass
        raise
    finally:
        watchdog.cancel()
        report["watchdogFired"] = watchdog_fired.is_set()
        report["elapsedSeconds"] = round(time.monotonic() - started, 2)
        if process.poll() is None:
            try:
                command("quit")
                process.wait(timeout=15)
            except (RuntimeError, subprocess.TimeoutExpired, json.JSONDecodeError):
                process.kill()
                process.wait(timeout=10)
        # Shutdown callbacks can log after the managed console's final query.
        native_log = (LOGS / ("standalone-client" + suffix + ".log")).read_text(encoding="utf-8", errors="replace")
        report["nativeWarningCallsIncludingShutdown"] = sum("UnityEngine.Debug:LogWarning" in line for line in native_log.splitlines())
        # Engine serialization/importer messages can bypass both managed counters
        # and Debug.LogWarning. Reject them in the player as well as GPU errors.
        native_failure_markers = (
            "ERROR: Shader ", "Shader error in ", "UnityEngine.Debug:LogError", "UnityEngine.Debug:LogException",
            "Releasing render texture that is set to be RenderTexture.active",
            "before 2019.1 are deprecated", "below the supported minimum",
            "Instantiating a non-readable", "BoxCollider does not support negative scale",
            "The referenced script on this Behaviour", "Serialization layout mismatch",
            "NullReferenceException:", "MissingReferenceException:", "MissingMethodException:",
            "TypeLoadException:", "InvalidOperationException:", "ArgumentException:",
            "IndexOutOfRangeException:", "DllNotFoundException:", "FileNotFoundException:",
        )
        report["nativeProblemsIncludingShutdown"] = list(dict.fromkeys(
            line for line in native_log.splitlines() if any(marker in line for marker in native_failure_markers)))
        shutdown_failed = report["result"] == "passed" and (
            report["nativeWarningCallsIncludingShutdown"] > 0 or report["nativeProblemsIncludingShutdown"])
        if shutdown_failed:
            report["result"] = "failed"
            report["failure"] = "Native log contains warnings or engine failures, including shutdown; inspect its full log"
        (LOGS / ("standalone-smoke" + suffix + ".json")).write_text(json.dumps(report, indent=2), encoding="utf-8")
        if shutdown_failed:
            raise RuntimeError(report["failure"])


if __name__ == "__main__":
    main()
