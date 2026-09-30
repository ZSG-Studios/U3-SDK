"""Qualify a built port and collect persistent diagnostics without touching the Editor UI."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

PROJECT = Path(__file__).resolve().parents[1]
LOGS = PROJECT / "Logs"
MAPS = ("Tutorial", "PEI", "Russia", "Germany", "Washington", "Yukon", "Alpha Valley", "Destruction", "Monolith", "Paintball_Arena_0")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", nargs="+", choices=MAPS, default=list(MAPS))
    parser.add_argument("--apis", nargs="+", choices=("dx12", "vulkan"), default=["dx12", "vulkan"])
    args = parser.parse_args()
    LOGS.mkdir(exist_ok=True)
    status_path = PROJECT / "Temp" / "pipeline_build_status.json"
    build = json.loads(status_path.read_text(encoding="utf-8-sig"))
    build_gate = (build.get("status") == "completed" and build.get("result") == "Succeeded"
                  and build.get("totalErrors") == 0 and build.get("totalWarnings") == 0)
    report = {"startedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
              "build": {key: build.get(key) for key in ("buildId", "status", "result", "totalErrors", "totalWarnings")},
              "buildPassed": build_gate, "expectedCases": len(args.maps) * len(args.apis), "complete": False, "cases": [], "passed": False}
    output = LOGS / "port-qualification.json"
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    if not build_gate:
        print("Build qualification rejected: " + json.dumps(report["build"]), flush=True)
        return 1
    for api in args.apis:
        for map_name in args.maps:
            suffix = "-" + api + ("" if map_name == "Tutorial" else "-" + map_name.lower())
            cli_log = LOGS / ("qualification" + suffix + ".log")
            print(f"Checking {map_name} on {api}", flush=True)
            started = time.monotonic()
            with cli_log.open("w", encoding="utf-8") as stream:
                try:
                    result = subprocess.run([sys.executable, str(PROJECT / "Tools" / "smoke_client.py"),
                                             "--map", map_name, "--graphics-api", api],
                                            cwd=PROJECT, stdout=stream, stderr=subprocess.STDOUT, timeout=330)
                    exit_code = result.returncode
                except subprocess.TimeoutExpired:
                    # smoke_client owns and terminates its player within its own 240-second watchdog.
                    exit_code = 124
            smoke = LOGS / ("standalone-smoke" + suffix + ".json")
            case = {"map": map_name, "api": api, "exitCode": exit_code, "elapsedSeconds": round(time.monotonic()-started, 2),
                    "reportPath": str(smoke), "cliLogPath": str(cli_log), "passed": False}
            if smoke.is_file():
                detail = json.loads(smoke.read_text(encoding="utf-8-sig"))
                case["passed"] = exit_code == 0 and detail.get("result") == "passed"
                case["console"] = detail.get("console")
            report["cases"].append(case)
            report["complete"] = len(report["cases"]) == report["expectedCases"]
            report["passed"] = report["complete"] and all(c["passed"] for c in report["cases"])
            output.write_text(json.dumps(report, indent=2), encoding="utf-8")
            print(json.dumps(case), flush=True)
    report["finishedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
