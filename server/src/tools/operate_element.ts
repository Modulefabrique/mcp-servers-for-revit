import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerOperateElementTool(server: McpServer) {
  defineRevitTool(server, {
    name: "operate_element",
    title: "Select, show, hide, colour or delete elements",
    // Destructief vanwege de actie Delete; de overige acties wijzigen alleen selectie/weergave
    kind: "destructive",
    description:
      "Perform one action on a list of Revit elements, mostly in the ACTIVE view. Take the element ids " +
      "from ai_element_filter, get_selected_elements or get_current_view_elements; never invent ids. " +
      "Actions (case-insensitive): " +
      "Select - make the elements the current selection in Revit. " +
      "SelectionBox - set a section box around the elements (+1 ft margin) in the active 3D view, or " +
      "switch to the default {3D} view first if the active view is not 3D. " +
      "SetColor - override projection/cut line and surface pattern colour in the active view (colorValue). " +
      "SetTransparency - override surface transparency in the active view (transparencyValue). " +
      "Hide / Unhide - permanently hide or unhide the elements in the active view. " +
      "TempHide / Isolate - temporary hide or isolate in the active view; ResetIsolate ends temporary " +
      "hide/isolate (elementIds may be empty for ResetIsolate). " +
      "Delete - remove the elements from the model. Delete is SAFE BY DEFAULT: unless dryRun=false is " +
      "passed it only returns a preview (requested elements plus the dependent elements Revit would " +
      "delete with them) and changes nothing. Show the preview to the user, ask for confirmation, and " +
      "only then repeat the call with dryRun=false. For deleting, delete_element does the same. " +
      "Each action except Select runs in its own Revit transaction.",
    schema: {
      data: z
        .object({
          elementIds: z
            .array(z.number().describe("A Revit element id"))
            .describe("Revit element ids to perform the action on"),
          action: z
            .string()
            .describe(
              "The action: Select, SelectionBox, SetColor, SetTransparency, Delete, Hide, TempHide, " +
                "Isolate, Unhide or ResetIsolate."
            ),
          transparencyValue: z
            .number()
            .default(50)
            .describe("Only for SetTransparency: surface transparency 0-100 (higher = more transparent)."),
          colorValue: z
            .array(z.number())
            .default([255, 0, 0])
            .describe("Only for SetColor: RGB values 0-255, e.g. [255, 0, 0] for red (the default)."),
          dryRun: z
            .boolean()
            .optional()
            .describe(
              "Only for Delete. Omitted or true: preview only, nothing is deleted. false: really " +
                "delete - only after the user has confirmed the preview. Ignored by other actions."
            ),
        })
        .describe("The elements and the action to perform on them"),
    },
  });
}
