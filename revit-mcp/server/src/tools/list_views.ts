import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerListViewsTool(server: McpServer) {
  server.tool(
    "list_views",
    "List all views in the Revit model (floor plans, ceiling plans, sections, elevations, 3D views, etc.), excluding sheets and internal/browser views. Returns each view's name, view type, element ID, whether it is a view template, and the associated level name (if any).",
    {},
    async (_args, _extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("list_views", {});
        });

        return {
          content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `list_views failed: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
        };
      }
    }
  );
}
