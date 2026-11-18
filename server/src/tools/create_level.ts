import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreateLevelTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_level",
    title: "Create levels",
    kind: "write",
    // Een peil met een bestaande naam wordt niet opnieuw aangemaakt, dus herhalen heeft geen extra effect
    idempotent: true,
    description:
      "Create one or more levels at the given elevations (millimeters), each in its own transaction, " +
      "optionally with a floor plan and a ceiling plan view (using the first floor plan / ceiling plan view " +
      "type of the project; the views get Revit's default names). A level whose name already exists " +
      "(case-insensitive) is not created again: the existing level is returned with AlreadyExisted=true and " +
      "its actual elevation, so repeating a call is safe. Only name, elevation, isBuildingStory, " +
      "createFloorPlan and createCeilingPlan are used; description, isMainLevel, computationHeight and the " +
      "view offsets are currently ignored. A level that fails is reported as a warning in Message and the " +
      "others still go through. Returns Success, Message (including warnings) and Response = per level Id, " +
      "UniqueId, Name, Elevation (mm), FloorPlanViewName, CeilingPlanViewName and AlreadyExisted.",
    schema: {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .describe("Name of the level (e.g. 'Level 2', 'Roof'); must be unique in the project"),
            elevation: z.number().describe("Elevation of the level in mm"),
            description: z.string().optional().describe("Currently ignored"),
            isMainLevel: z.boolean().default(true).describe("Currently ignored"),
            isBuildingStory: z
              .boolean()
              .default(true)
              .describe("Whether the level is a building story (default: true)"),
            computationHeight: z.number().optional().describe("Currently ignored"),
            viewPlanOffset: z.number().optional().describe("Currently ignored"),
            viewSectionOffset: z.number().optional().describe("Currently ignored"),
            viewElevationOffset: z.number().optional().describe("Currently ignored"),
            createFloorPlan: z
              .boolean()
              .default(true)
              .describe("Also create a floor plan view for this level (default: true)"),
            createCeilingPlan: z
              .boolean()
              .default(true)
              .describe("Also create a ceiling plan view for this level (default: true)"),
          })
        )
        .describe("Levels to create"),
    },
    buildParams: (args) => ({
      data: args.data,
    }),
  });
}
