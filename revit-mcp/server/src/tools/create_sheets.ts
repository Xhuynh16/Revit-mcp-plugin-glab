import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateSheetsTool(server: McpServer) {
  server.tool(
    "create_sheets",
    "Batch-create drawing sheets in Revit, optionally placing views on each sheet. " +
      "Each sheet needs a unique sheet number (e.g. 'A-101') and a name; generate sensible numbering sequences yourself when the user asks for it. " +
      "Views (use list_views to find IDs) are auto-arranged in a grid on the sheet; schedules are supported too. " +
      "A view that is already on another sheet cannot be placed again. " +
      "titleBlockTypeId is optional — when omitted, the first title block type in the project is used " +
      "(use get_available_family_types with category 'OST_TitleBlocks' to list available title blocks).",
    {
      titleBlockTypeId: z
        .number()
        .optional()
        .describe(
          "Element ID of the title block type to use for all sheets. Omit to use the project's first title block type."
        ),
      sheets: z
        .array(
          z.object({
            number: z
              .string()
              .describe("Unique sheet number, e.g. 'A-101'"),
            name: z.string().describe("Sheet name/title, e.g. 'Floor Plan - Level 1'"),
            viewIds: z
              .array(z.number())
              .optional()
              .describe("Element IDs of views to place on this sheet (auto-arranged in a grid)"),
          })
        )
        .describe("Sheets to create, in order"),
    },
    async (args, _extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_sheets", {
            titleBlockTypeId: args.titleBlockTypeId,
            sheets: args.sheets.map((s) => ({
              number: s.number,
              name: s.name,
              viewIds: s.viewIds ?? [],
            })),
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
              text: `create_sheets failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
