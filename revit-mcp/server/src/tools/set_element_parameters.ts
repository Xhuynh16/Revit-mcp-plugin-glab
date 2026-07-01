import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetElementParametersTool(server: McpServer) {
  server.tool(
    "set_element_parameters",
    "Batch-set one or more parameter values on one or more Revit elements. " +
      "Use get_element_properties or ai_element_filter first to find element IDs and confirm parameter names. " +
      "Values should be given in the same format/units a user would type into Revit's UI " +
      "(e.g. '3000' for a length parameter shown in mm, 'C-01' for a text Mark, 'Yes'/'No' for a checkbox). " +
      "Read-only parameters and ElementId-typed parameters are not supported and will be reported as failed.",
    {
      elementIds: z
        .array(z.number())
        .describe("Element IDs (integers) of the elements to update"),
      parameters: z
        .array(
          z.object({
            name: z.string().describe("Parameter name as shown in Revit (e.g. 'Mark', 'Comments')"),
            value: z.string().describe("New value as a string, in Revit UI display format/units"),
          })
        )
        .describe("List of parameter name/value pairs to apply to every element in elementIds"),
    },
    async (args, _extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_parameters", {
            elementIds: args.elementIds,
            parameters: args.parameters,
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
              text: `set_element_parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
