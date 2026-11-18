import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreateLineBasedElementTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_line_based_element",
    title: "Create line-based elements",
    kind: "write",
    description:
      "Create walls, ducts or line-based family instances (e.g. structural framing beams) along a line, one " +
      "transaction per element. Coordinates and lengths are in millimeters, in model coordinates relative to " +
      "Revit's internal origin. The type comes from typeId when it is a valid wall type, duct type or family " +
      "type id (from get_available_family_types; never invent ids); otherwise the first available type of " +
      "'category' is used, with a warning in Message if a typeId was given but not found. Level: the element " +
      "is associated with the existing level nearest to baseLevel (an elevation in mm, not a level id). " +
      "Walls: base at baseLevel + baseOffset, height is the unconnected height, thickness is ignored (the " +
      "wall type determines it). Ducts: offset at baseLevel + baseOffset, created in the project's first " +
      "Supply Air system type (nothing is created if there is none), height is ignored. Other families: the " +
      "3D line is passed to Revit as is, so give p0/p1 the intended Z. Elements with an unknown category and no valid typeId are " +
      "skipped without a warning, so compare the returned ids with what you requested. If an element throws " +
      "an error, processing stops; elements created before it remain in the model. Returns Success, Message " +
      "(including warnings) and Response = list of the created element ids.",
    schema: {
      data: z
        .array(
          z.object({
            category: z
              .string()
              .describe(
                "BuiltInCategory name, e.g. OST_Walls, OST_StructuralFraming, OST_DuctCurves. Ignored when typeId is a valid type id"
              ),
            typeId: z
              .number()
              .optional()
              .describe(
                "Element id of the wall type, duct type or family type to create (from get_available_family_types)"
              ),
            locationLine: z
              .object({
                p0: z.object({
                  x: z.number().describe("X coordinate of start point in mm"),
                  y: z.number().describe("Y coordinate of start point in mm"),
                  z: z.number().describe("Z coordinate of start point in mm"),
                }),
                p1: z.object({
                  x: z.number().describe("X coordinate of end point in mm"),
                  y: z.number().describe("Y coordinate of end point in mm"),
                  z: z.number().describe("Z coordinate of end point in mm"),
                }),
              })
              .describe("Location line of the element, from p0 to p1 (mm)"),
            thickness: z
              .number()
              .describe("Currently ignored: the thickness/width comes from the element type"),
            height: z
              .number()
              .describe("Height in mm (for walls the unconnected height; ignored for ducts)"),
            baseLevel: z
              .number()
              .describe(
                "Elevation in mm used to pick the nearest existing level (not a level id)"
              ),
            baseOffset: z
              .number()
              .describe("Offset in mm of the element base above baseLevel"),
          })
        )
        .describe("Line-based elements to create"),
    },
  });
}
