import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerSetParametersTool(server: McpServer) {
  defineRevitTool(server, {
    name: "set_parameters",
    title: "Set parameter values",
    kind: "destructive",
    // Dezelfde waarde nogmaals zetten verandert niets
    idempotent: true,
    description:
      "Set one or more parameter values on one or more Revit elements in a single call. Always pass a " +
      "list of elements, even for a single element (use a list with one entry). Take the element ids " +
      "from ai_element_filter, get_selected_elements, get_current_view_elements or get_parameters; never " +
      "invent ids. Parameters are looked up by name (same as Revit's Properties panel), only on the given " +
      "element itself: to change a type parameter, pass the type's ElementId (TypeId), which changes it " +
      "for all elements of that type. Use get_available_parameters / get_parameters first to find valid " +
      "names and current values. Numeric values for length/area/volume parameters must be given in the " +
      "model's display units (e.g. millimeters), not Revit's internal feet - the same units get_parameters " +
      "returns. Send numeric values (numbers, Yes/No as true/false, ElementIds for element references) as " +
      "JSON numbers/booleans, not as strings; send values for text parameters as strings. Returns, per " +
      "element and per parameter, whether the change succeeded (and why not if it failed) plus OldValue " +
      "and NewValue as shown in the Properties panel. Set dryRun=true to preview: the changes are applied " +
      "and immediately rolled back, so you see per parameter whether it would succeed and the resulting " +
      "value without changing the model - useful before large or risky batch changes. If the call fails " +
      "with a timeout, nothing was changed; split the elements list into smaller batches (e.g. 50-100 " +
      "elements at a time) and retry.",
    schema: {
      elements: z
        .array(
          z.object({
            elementId: z.string().describe("Revit ElementId of the element to update, as a string"),
            parameters: z
              .array(
                z.object({
                  name: z
                    .string()
                    .describe("Parameter name, as shown in Revit's Properties panel"),
                  value: z
                    .union([z.string(), z.number(), z.boolean()])
                    .describe("New value for this parameter"),
                })
              )
              .min(1)
              .describe("Parameter changes to apply to this element"),
          })
        )
        .min(1)
        .describe(
          "Elements to update, each with its own list of parameter changes. Use a single-item list for one element."
        ),
      dryRun: z
        .boolean()
        .default(false)
        .describe(
          "false (default): apply the changes. true: preview only - apply and immediately roll back, " +
            "nothing changes in the model."
        ),
    },
    buildParams: (args) => ({
      elements: args.elements,
      dryRun: args.dryRun,
    }),
  });
}
