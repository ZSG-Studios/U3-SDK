"""Launch the built DX12 client visibly, load Germany, and leave it running."""
import json
import subprocess
import time

from smoke_client import BUILD, LOGS, capture, command, evaluate, state


def main():
    LOGS.mkdir(exist_ok=True)
    process = subprocess.Popen(
        [str(BUILD / "Unturned.exe"), "-force-d3d12", "-NoWorkshopSubscriptions",
         "-FrameRateLimit", "60", "-screen-fullscreen", "0", "-screen-width", "1280",
         "-screen-height", "720", "-logFile", str(LOGS / "interactive-client-dx12.log")],
        cwd=BUILD, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    report = {"pid": process.pid, "map": "Germany", "result": "failed"}
    started = time.monotonic()
    phase = "menu"
    settled = None
    try:
        while time.monotonic() - started < 180:
            if process.poll() is not None:
                raise RuntimeError(f"Client exited: {process.returncode}")
            try:
                current = state()
            except (RuntimeError, subprocess.TimeoutExpired, json.JSONDecodeError):
                time.sleep(3)
                continue
            ready = not current["assetsLoading"] and not current["loadingBlocked"]
            if phase == "menu" and current["scene"] == "Menu" and ready:
                capture("interactive-menu-dx12.png")
                report["menu"] = state()
                evaluate('SDG.Unturned.OptionsSettings.pauseWhenUnfocused = false; '
                         'UnityEngine.Time.timeScale = 1; SDG.Unturned.Provider.map = "Germany"; '
                         'SDG.Unturned.Provider.singleplayer(SDG.Unturned.EGameMode.NORMAL, false); return true;')
                phase = "gameplay"
            elif phase == "gameplay" and ready and current["levelLoaded"] and current["playerPresent"]:
                if settled is None:
                    settled = (time.monotonic(), current["frame"])
                elif time.monotonic() - settled[0] >= 10 and current["frame"] > settled[1]:
                    report["gameplay"] = current
                    capture("interactive-gameplay-dx12.png")
                    report["console"] = command("console_status")
                    if current["graphicsApi"] != "Direct3D12" or current["pipeline"] != "UniversalRenderPipeline":
                        raise RuntimeError("Unexpected graphics API or pipeline")
                    if report["console"]["counts"]["error"] or report["console"]["counts"]["warn"]:
                        raise RuntimeError("Inspect persistent diagnostics: game logged errors or warnings")
                    report["result"] = "running"
                    print(json.dumps(report), flush=True)
                    return
            time.sleep(3)
        raise TimeoutError("Visible launch did not reach gameplay within 180 seconds")
    except Exception as error:
        report["failure"] = str(error)
        if process.poll() is None:
            process.kill()
            process.wait(timeout=10)
        raise
    finally:
        report["elapsedSeconds"] = round(time.monotonic() - started, 2)
        (LOGS / "interactive-launch.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
