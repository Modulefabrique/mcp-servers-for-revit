import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreateGridTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_grid",
    title: "Create grid system",
    kind: "write",
    description:
      "Create a rectangular system of grid lines in one transaction. All values are in millimeters, in " +
      "model coordinates relative to Revit's internal origin. 'X-axis' grids are xCount vertical lines " +
      "(parallel to the Y axis) at X = xStartPosition + i * xSpacing, running from yExtentMin to " +
      "yExtentMax; 'Y-axis' grids are yCount horizontal lines (parallel to the X axis) at Y = " +
      "yStartPosition + i * ySpacing, running from xExtentMin to xExtentMax. Labels: 'alphabetic' starts " +
      "at the first letter of the start label (A, B, ... Z, AA, AB, ...), 'numeric' counts up from the " +
      "start label's number (1 if it is not a number). A label that is already used by an existing grid " +
      "(case-insensitive) gets a number appended (e.g. 'A1') and is reported with wasRenamed=true. Calling " +
      "the tool again creates a second set of grids, so check for existing grids first (e.g. ai_element_filter " +
      "with filterCategory 'OST_Grids'). Returns Success, Message and Response = per grid elementId, name, " +
      "originalName, wasRenamed, axis ('X' or 'Y') and position (mm).",
    schema: {
      xCount: z
        .number()
        .int()
        .positive()
        .describe("Number of X-axis grid lines (vertical lines, parallel to the Y axis)"),
      xSpacing: z
        .number()
        .positive()
        .describe("Distance between consecutive X-axis grid lines in mm"),
      xStartLabel: z
        .string()
        .default("A")
        .describe("Label of the first X-axis grid (e.g. 'A' or '1')"),
      xNamingStyle: z
        .enum(["alphabetic", "numeric"])
        .default("alphabetic")
        .describe("Naming style for X-axis grids: 'alphabetic' (A, B, C...) or 'numeric' (1, 2, 3...)"),
      yCount: z
        .number()
        .int()
        .positive()
        .describe("Number of Y-axis grid lines (horizontal lines, parallel to the X axis)"),
      ySpacing: z
        .number()
        .positive()
        .describe("Distance between consecutive Y-axis grid lines in mm"),
      yStartLabel: z
        .string()
        .default("1")
        .describe("Label of the first Y-axis grid (e.g. '1' or 'A')"),
      yNamingStyle: z
        .enum(["alphabetic", "numeric"])
        .default("numeric")
        .describe("Naming style for Y-axis grids: 'alphabetic' (A, B, C...) or 'numeric' (1, 2, 3...)"),
      xExtentMin: z
        .number()
        .default(0)
        .describe("X coordinate in mm where the Y-axis (horizontal) grid lines start"),
      xExtentMax: z
        .number()
        .default(50000)
        .describe("X coordinate in mm where the Y-axis (horizontal) grid lines end"),
      yExtentMin: z
        .number()
        .default(0)
        .describe("Y coordinate in mm where the X-axis (vertical) grid lines start"),
      yExtentMax: z
        .number()
        .default(50000)
        .describe("Y coordinate in mm where the X-axis (vertical) grid lines end"),
      elevation: z
        .number()
        .default(0)
        .describe("Z coordinate in mm of the grid lines"),
      xStartPosition: z
        .number()
        .default(0)
        .describe("X coordinate in mm of the first X-axis grid line"),
      yStartPosition: z
        .number()
        .default(0)
        .describe("Y coordinate in mm of the first Y-axis grid line"),
    },
    buildParams: (args) => ({
      xCount: args.xCount,
      xSpacing: args.xSpacing,
      xStartLabel: args.xStartLabel,
      xNamingStyle: args.xNamingStyle,
      yCount: args.yCount,
      ySpacing: args.ySpacing,
      yStartLabel: args.yStartLabel,
      yNamingStyle: args.yNamingStyle,
      xExtentMin: args.xExtentMin,
      xExtentMax: args.xExtentMax,
      yExtentMin: args.yExtentMin,
      yExtentMax: args.yExtentMax,
      elevation: args.elevation,
      xStartPosition: args.xStartPosition,
      yStartPosition: args.yStartPosition,
    }),
  });
}
