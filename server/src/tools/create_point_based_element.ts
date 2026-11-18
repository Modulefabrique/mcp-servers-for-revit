import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreatePointBasedElementTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_point_based_element",
    title: "Create point-based elements",
    kind: "write",
    description:
      "Place family instances at a point, such as doors, windows, furniture or columns, one transaction per " +
      "element. typeId is effectively required: it must be the id of a loadable family type from " +
      "get_available_family_types (never invent ids); elements without a valid typeId are skipped without a " +
      "warning ('name' is only a free label and does not select a category). Coordinates and lengths are in " +
      "millimeters, in model coordinates relative to Revit's internal origin. The instance is associated with " +
      "the existing level nearest to baseLevel (an elevation in mm, not a level id). baseOffset (mm above " +
      "baseLevel) is used as sill height for doors/windows and as base offset for two-level families such as " +
      "columns; height determines the top level/offset of two-level families. width and depth are currently " +
      "ignored: the family type determines the size. Doors and windows are hosted in hostWallId if given, " +
      "otherwise in the nearest wall, and are snapped onto the wall's location line; they face the side of " +
      "the wall where locationPoint lies, unless facingFlipped=true, which instead flips Revit's default " +
      "facing. rotation is applied only to elements other than doors and windows. If an element throws an " +
      "error (e.g. no host found for a hosted family), processing stops; elements created before it remain " +
      "in the model. Returns Success, Message (including warnings) and Response = list of the created " +
      "element ids.",
    schema: {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .describe("Free label for the element (e.g. 'door', 'window'); not used by Revit"),
            typeId: z
              .number()
              .optional()
              .describe(
                "Element id of the family type to place (from get_available_family_types). Required in practice"
              ),
            locationPoint: z
              .object({
                x: z.number().describe("X coordinate in mm"),
                y: z.number().describe("Y coordinate in mm"),
                z: z.number().describe("Z coordinate in mm"),
              })
              .describe(
                "Insertion point (mm). For doors/windows a point on the side of the wall the element should face"
              ),
            width: z.number().describe("Currently ignored: the family type determines the size"),
            depth: z.number().optional().describe("Currently ignored: the family type determines the size"),
            height: z
              .number()
              .describe("Height in mm; used for the top level/offset of two-level families such as columns"),
            baseLevel: z
              .number()
              .describe(
                "Elevation in mm used to pick the nearest existing level (not a level id)"
              ),
            baseOffset: z
              .number()
              .describe("Offset in mm above baseLevel (sill height for doors/windows)"),
            rotation: z
              .number()
              .optional()
              .describe(
                "Rotation in degrees about the vertical axis through locationPoint; ignored for doors and windows"
              ),
            hostWallId: z
              .number()
              .optional()
              .describe(
                "Element id of the wall to host a door/window. If omitted or not a wall, the nearest wall is used"
              ),
            facingFlipped: z
              .boolean()
              .optional()
              .default(false)
              .describe(
                "Doors/windows only: true flips Revit's default facing instead of facing the side of locationPoint"
              ),
          })
        )
        .describe("Point-based elements to create"),
    },
  });
}
