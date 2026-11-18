import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

// Fabrieksfunctie i.p.v. één gedeeld schema: hergebruik van dezelfde zod-instantie levert
// $ref-verwijzingen op in het JSON-schema, wat sommige clients slecht verwerken
const pointSchema = () =>
  z.object({
    x: z.number().describe("X coordinate in mm"),
    y: z.number().describe("Y coordinate in mm"),
    z: z.number().describe("Z coordinate in mm"),
  });

export function registerCreateDimensionsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_dimensions",
    title: "Create dimensions",
    kind: "write",
    description:
      "Create linear dimensions in a Revit view. Coordinates are in millimeters, in model coordinates " +
      "relative to Revit's internal origin. The dimension line is drawn through startPoint and endPoint, so " +
      "place those where the dimension line should appear, along the direction to measure. Each dimension " +
      "needs two references: with elementIds, one reference is taken from each element (for walls and " +
      "family instances the vertical planar face whose normal is most parallel to the start-to-end " +
      "direction; for other elements the element itself), so pass at least two ids, taken from " +
      "ai_element_filter, get_current_view_elements or get_selected_elements (never invent ids). Without " +
      "elementIds, the element nearest to startPoint and the element nearest to endPoint (within about " +
      "1500 mm, only elements with a location point or curve in the view) are used. A dimension for which " +
      "fewer than two references are found is skipped without an error, so compare the number of returned " +
      "ids with the number requested. Each dimension is committed in its own transaction; if one throws an " +
      "error, processing stops and dimensions created before it remain in the model. Returns Success, " +
      "Message and Response = list of the created dimension element ids.",
    schema: {
      dimensions: z
        .array(
          z.object({
            startPoint: pointSchema().describe(
              "Start point of the dimension line (mm); without elementIds also used to find the first reference"
            ),
            endPoint: pointSchema().describe(
              "End point of the dimension line (mm); without elementIds also used to find the second reference"
            ),
            linePoint: pointSchema()
              .optional()
              .describe(
                "Currently ignored by the Revit add-in: the dimension line always runs through startPoint and endPoint"
              ),
            elementIds: z
              .array(z.number())
              .optional()
              .describe(
                "Element ids to dimension between (at least two). If omitted or empty, references are searched near startPoint and endPoint"
              ),
            dimensionType: z
              .string()
              .optional()
              .default("Linear")
              .describe("Currently ignored: only linear dimensions are created"),
            dimensionStyleId: z
              .number()
              .optional()
              .default(-1)
              .describe(
                "Element id of the dimension type (style) to apply; -1 keeps Revit's default dimension type"
              ),
            viewId: z
              .number()
              .optional()
              .default(-1)
              .describe(
                "Element id of the view to create the dimension in; -1 for the active view. Must be a view that can hold dimensions (plan, section, elevation)"
              ),
          })
        )
        .describe("Dimensions to create"),
    },
    buildParams: (args) => ({
      dimensions: args.dimensions,
    }),
  });
}
