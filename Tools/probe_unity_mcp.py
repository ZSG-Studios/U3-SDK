# /// script
# requires-python = ">=3.12"
# dependencies = ["mcp==2.2.0"]
# ///
"""Probe Unity's official MCP servers using the standard MCP Python client.

Run with: uv run Tools/probe_unity_mcp.py
Use --issue-tracker to inspect Unity's public Issue Tracker MCP instead.
"""
import argparse
import asyncio
import json
from pathlib import Path
import shutil

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from mcp.client.streamable_http import streamable_http_client


async def inspect_session(read, write, project, issue_tracker, call=None, arguments=None):
    async with ClientSession(read, write) as session:
        await session.initialize()
        result = await session.list_tools()
        catalog = [tool.model_dump(mode="json") for tool in result.tools]
        output = project / "Logs" / ("issue-tracker-mcp-tools.json" if issue_tracker else "unity-mcp-tools.json")
        output.parent.mkdir(exist_ok=True)
        output.write_text(json.dumps(catalog, indent=2), encoding="utf-8")
        print(json.dumps({"toolCount": len(catalog), "catalog": str(output)}))
        if not catalog:
            raise RuntimeError("The MCP server exposes no tools; wait for the Editor's Pipeline server")
        if not issue_tracker:
            for name in ("editor_status", "console_status"):
                if any(tool.name == name for tool in result.tools):
                    response = await session.call_tool(name, {})
                    is_error = response.is_error
                    print(json.dumps({"tool": name, "isError": is_error, "content": [item.model_dump(mode="json") for item in response.content]}))
                    if is_error:
                        raise RuntimeError(f"MCP tool {name} failed")
        if call:
            response = await session.call_tool(call, arguments or {})
            result_path = project / "Logs" / f"mcp-{call}-result.json"
            result_path.write_text(response.model_dump_json(by_alias=True, indent=2), encoding="utf-8")
            print(json.dumps({"tool": call, "isError": response.is_error, "result": str(result_path)}))
            for item in response.content:
                if item.type == "text":
                    print(item.text[:2500])
            if response.is_error:
                raise RuntimeError(f"MCP tool {call} failed")


async def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--issue-tracker", action="store_true")
    parser.add_argument("--call", help="An exposed tool to invoke after discovery")
    parser.add_argument("--arguments-file", type=Path, help="JSON tool arguments")
    options = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    arguments = json.loads(options.arguments_file.read_text(encoding="utf-8")) if options.arguments_file else {}
    async with asyncio.timeout(120):
        if options.issue_tracker:
            async with streamable_http_client("https://issuetracker-mcp.unity.com/mcp") as streams:
                await inspect_session(streams[0], streams[1], project, True, options.call, arguments)
        else:
            executable = shutil.which("unity")
            if not executable:
                raise RuntimeError("Unity CLI is not on PATH")
            params = StdioServerParameters(command=executable, args=["mcp", "--project-path", str(project)])
            async with stdio_client(params) as (read, write):
                await inspect_session(read, write, project, False, options.call, arguments)


if __name__ == "__main__":
    asyncio.run(main())
