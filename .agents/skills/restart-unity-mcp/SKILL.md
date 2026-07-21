---
name: restart-unity-mcp
description: Use when the Unity MCP server is disconnected, unresponsive, or randomly stopping. This skill restarts the mcp-for-unity background process.
---

# Restart Unity MCP Server

When the Unity MCP server stops responding or disconnects, follow these steps to restart it using PowerShell:

1. Stop any currently running instance of the Unity MCP server to prevent port conflicts:
   ```powershell
   Get-CimInstance Win32_Process | Where-Object CommandLine -match "mcp-for-unity" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -PassThru }
   ```

2. Start the Unity MCP server in the background using the `run_command` tool. Important: You must use `Start-Process` with `-WindowStyle Hidden` or `-NoNewWindow` and return immediately.
   ```powershell
   Start-Process -NoNewWindow -FilePath "C:\Users\lance\.local\bin\uvx.exe" -ArgumentList "--offline --from `"mcpforunityserver==10.1.0`" mcp-for-unity --transport http --http-port 8080"
   ```

3. Wait 2 seconds and use the `call_mcp_tool` tool to verify the server is back online by calling `manage_editor` with `{"action": "get_play_mode_state"}` on the `unityMCP` server.
