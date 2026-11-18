import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerColorElementsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "color_elements",
    title: "Color elements by parameter value",
    kind: "write",
    method: "color_splash",
    description:
      "Color-code the elements of one category in the ACTIVE view by the value of a parameter. Elements " +
      "of the category visible in the active view are grouped by their value of parameterName (looked up " +
      "on the element first, then on its type; elements without a value form the group 'None'), and each " +
      "group gets a solid-fill projection and cut color override in the active view only. This replaces " +
      "any existing per-element graphic overrides of those elements in that view; the model elements " +
      "themselves are not changed. The active view must support graphic overrides, otherwise an error is " +
      "returned. Colors are random by default (so a repeated call gives different colors), a blue-to-red " +
      "gradient with useGradient=true, or taken from customColors, which are assigned in order to the " +
      "groups in the order they were found (further groups get random colors). Returns a summary with the " +
      "number of colored elements and, per value group, the element count and RGB color.",
    schema: {
      categoryName: z
        .string()
        .describe(
          "Category name as shown in Revit's UI (e.g. 'Walls', 'Doors', 'Rooms', or the localized name such " +
            "as 'Wanden' in a Dutch Revit), matched case-insensitively. Not a BuiltInCategory name like OST_Walls."
        ),
      parameterName: z
        .string()
        .describe(
          "Name of the parameter whose value determines the color group, as shown in Revit's Properties panel"
        ),
      useGradient: z
        .boolean()
        .optional()
        .default(false)
        .describe("Use a blue-to-red gradient over the value groups instead of random colors"),
      customColors: z
        .array(
          z.object({
            r: z.number().int().min(0).max(255),
            g: z.number().int().min(0).max(255),
            b: z.number().int().min(0).max(255),
          })
        )
        .optional()
        .describe(
          "RGB colors (0-255) assigned in order to the value groups; takes precedence over useGradient"
        ),
    },
    formatResult: (response) => {
      // Zelfde tekstopmaak als vóór de omzetting naar defineRevitTool
      if (!response.success) {
        return `Color operation failed: ${response.message}`;
      }

      const coloredGroups = response.results || [];
      let resultText = `Successfully colored ${response.totalElements} elements across ${response.coloredGroups} groups.\n\n`;
      resultText += "Parameter Value Groups:\n";
      coloredGroups.forEach((group: any) => {
        const rgb = group.color;
        resultText += `- "${group.parameterValue}": ${group.count} elements colored with RGB(${rgb.r}, ${rgb.g}, ${rgb.b})\n`;
      });
      return resultText;
    },
  });
}
