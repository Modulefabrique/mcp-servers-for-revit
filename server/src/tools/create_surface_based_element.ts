import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreateSurfaceBasedElementTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_surface_based_element",
    title: "Create floors, ceilings or roofs",
    kind: "write",
    description:
      "Create floors, ceilings or flat roofs from a closed boundary, one transaction per element. Boundary " +
      "coordinates are in millimeters, in model coordinates relative to Revit's internal origin; the " +
      "outerLoop segments must form one closed, contiguous loop (each segment's p1 is the next segment's " +
      "p0). Only the outer loop is used (no openings). The type comes from typeId when it is a valid floor, " +
      "ceiling or roof type id (from get_available_family_types, e.g. categoryList ['OST_Floors']; never " +
      "invent ids); otherwise the first type of 'category' is used, with a warning in Message if a typeId " +
      "was given but not found. Elements with neither a valid typeId nor a category are skipped without a " +
      "warning ('name' is only a free label). Level: the existing level nearest to baseLevel (an elevation in " +
      "mm, not a level id); the element's height offset from that level is set so that it lies at baseLevel " +
      "+ baseOffset. thickness is ignored: the type determines the thickness. Roofs are created as footprint " +
      "roofs without sloped edges. If an element throws an error (e.g. an open boundary), processing stops; " +
      "elements created before it remain in the model. Returns Success, Message (including warnings) and " +
      "Response = list of the created element ids.",
    schema: {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .describe("Free label for the element (e.g. 'floor', 'ceiling'); not used by Revit"),
            category: z
              .enum(["OST_Floors", "OST_Ceilings", "OST_Roofs"])
              .optional()
              .describe(
                "OST_Floors, OST_Ceilings or OST_Roofs. Required unless typeId is a valid floor/ceiling/roof type id"
              ),
            typeId: z
              .number()
              .optional()
              .describe("Element id of the floor, ceiling or roof type (from get_available_family_types)"),
            boundary: z
              .object({
                outerLoop: z
                  .array(
                    z.object({
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
                  )
                  .min(3)
                  .describe("Line segments of the closed boundary, in order (mm)"),
              })
              .describe("Boundary of the element"),
            thickness: z
              .number()
              .describe("Currently ignored: the type determines the thickness"),
            baseLevel: z
              .number()
              .describe(
                "Elevation in mm used to pick the nearest existing level (not a level id)"
              ),
            baseOffset: z
              .number()
              .describe("Offset in mm above baseLevel"),
          })
        )
        .describe("Surface-based elements to create"),
    },
  });
}
