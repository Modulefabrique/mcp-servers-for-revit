import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetParametersTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_parameters",
    title: "Read parameter values",
    kind: "read",
    description:
      "Read the values of specific parameters on one or more Revit elements. Always pass a list of " +
      "elements, even for a single element; take the element ids from ai_element_filter, " +
      "get_selected_elements or get_current_view_elements (never invent ids). Specify which parameters to " +
      "read either per element (elements[].parameterNames) or once for all elements (parameterNames); a " +
      "per-element list overrides the common one, and every element must end up with at least one name. " +
      "Use get_available_parameters first to find valid parameter names. " +
      "Each parameter is looked up on the element itself first, then on its type (see 'Source'). " +
      "To change a parameter with Source 'type' via set_parameters, use the element's 'TypeId' as " +
      "elementId (this changes it for all elements of that type). " +
      "'Value' is a number in the model's display units (see 'Unit', e.g. millimeters) for " +
      "measurable values - the same units set_parameters expects - a boolean for Yes/No parameters, " +
      "an ElementId number for element references, or text; 'DisplayValue' is the value as shown in " +
      "Revit's Properties panel. Changes nothing.",
    schema: {
      elements: z
        .array(
          z.object({
            elementId: z.string().describe("Revit ElementId of the element to read, as a string"),
            parameterNames: z
              .array(z.string())
              .optional()
              .describe(
                "Parameter names to read for this element; overrides the common parameterNames"
              ),
          })
        )
        .min(1)
        .describe("Elements to read. Use a single-item list for one element."),
      parameterNames: z
        .array(z.string())
        .optional()
        .describe(
          "Parameter names to read for every element that has no parameterNames of its own"
        ),
    },
    buildParams: (args) => ({
      elements: args.elements,
      parameterNames: args.parameterNames,
    }),
  });
}
