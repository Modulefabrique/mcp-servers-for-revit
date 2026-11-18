import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreateStructuralFramingSystemTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_structural_framing_system",
    title: "Create beam system",
    kind: "write",
    description:
      "Create a Revit beam system that fills a rectangle with parallel structural framing beams at a fixed " +
      "spacing, in one transaction. All coordinates and distances are in millimeters, in model coordinates " +
      "relative to Revit's internal origin. Height: 'elevation' is the absolute height of the beam system; " +
      "it is placed on the existing level nearest to that elevation and the beams get a z-offset for the " +
      "difference. levelName is required but currently NOT used to choose the level, and no level is " +
      "created; the level actually used is reported in the result. directionEdge selects the edge of the " +
      "rectangle that is passed to Revit as the beam system's direction edge ('bottom'/'top' lie along X, " +
      "'left'/'right' along Y); Revit lays out the beams parallel to that edge, at 'spacing' measured " +
      "perpendicular to it. beamTypeName is matched as a case-insensitive part of the type name, family name " +
      "or 'Family: Type'; without a match the first structural framing type is used. Requires at least one " +
      "structural framing family loaded in the project, and works best with a floor or structural plan as " +
      "the active view. Each call creates a new beam system. Returns Success, Message (summary and warnings) " +
      "and Response = beamSystemId, beamIds, beamCount, actualSpacing (mm), usedBeamTypeName and levelName.",
    schema: {
      levelName: z
        .string()
        .describe(
          "Must be non-empty, but is currently not used: the level nearest to 'elevation' is used instead"
        ),
      xMin: z
        .number()
        .describe("Minimum X coordinate of the rectangular boundary in mm (must be less than xMax)"),
      xMax: z
        .number()
        .describe("Maximum X coordinate of the rectangular boundary in mm"),
      yMin: z
        .number()
        .describe("Minimum Y coordinate of the rectangular boundary in mm (must be less than yMax)"),
      yMax: z
        .number()
        .describe("Maximum Y coordinate of the rectangular boundary in mm"),
      spacing: z
        .number()
        .positive()
        .describe("Distance between beams in mm"),
      directionEdge: z
        .enum(["bottom", "right", "top", "left"])
        .default("bottom")
        .describe(
          "Edge of the rectangle used as the beam system's direction edge: 'bottom'/'top' (along X) or 'left'/'right' (along Y)"
        ),
      layoutRule: z
        .enum(["fixed_distance"])
        .default("fixed_distance")
        .describe("Layout rule; only 'fixed_distance' is supported"),
      justify: z
        .enum(["beginning", "center", "end", "directionline"])
        .default("center")
        .describe(
          "Justification of the beam layout within the boundary: 'center' (symmetric), 'beginning', 'end' or 'directionline'"
        ),
      beamTypeName: z
        .string()
        .optional()
        .describe(
          "Part of the beam type or family name (e.g. 'HEA200'). If omitted or not found, the first structural framing type is used"
        ),
      elevation: z
        .number()
        .default(0)
        .describe(
          "Absolute elevation of the beam system in mm; also determines the level used (the nearest existing level)"
        ),
      is3d: z
        .boolean()
        .default(false)
        .describe("Create a 3D beam system (for sloped or non-planar systems)"),
    },
    buildParams: (args) => ({
      levelName: args.levelName,
      xMin: args.xMin,
      xMax: args.xMax,
      yMin: args.yMin,
      yMax: args.yMax,
      spacing: args.spacing,
      directionEdge: args.directionEdge,
      layoutRule: args.layoutRule,
      justify: args.justify,
      beamTypeName: args.beamTypeName,
      elevation: args.elevation,
      is3d: args.is3d,
    }),
  });
}
