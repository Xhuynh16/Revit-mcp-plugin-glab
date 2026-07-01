import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementPropertiesTool(server: McpServer) {
  server.tool(
    "get_element_properties",
    "Get all parameters of a single element by its element ID. Returns the element's name, category, type name, and a list of all parameters with their name, value, storage type, parameter group, and read-only flag.",
    {
      elementId: z
        .number()
        .describe("The element ID (integer) of the element to inspect"),
    },
    async (args, _extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_properties", {
            elementId: args.elementId,
          });
        });

        return {
          content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `get_element_properties failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
