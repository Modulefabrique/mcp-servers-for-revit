import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerCreateRoomTool(server: McpServer) {
  defineRevitTool(server, {
    name: "create_room",
    title: "Create rooms",
    kind: "write",
    description:
      "Place rooms at given points, one transaction per room. location is in millimeters (model coordinates " +
      "relative to Revit's internal origin) and must lie inside an area enclosed by room-bounding elements on " +
      "the chosen level. Level: levelId if it is a valid level id, otherwise the level nearest to location.z, " +
      "otherwise the lowest level. Room numbers are kept unique: a requested number that already exists is " +
      "replaced by a free variant (compare Number with RequestedNumber), and without a number the next free " +
      "numeric number is used. A second call with the same input creates additional rooms. Verify the " +
      "result: rooms that could not be placed are missing from Response, and an Area of 0 means the room is " +
      "not enclosed. If a room throws an error, processing stops; rooms created before it remain in the " +
      "model. Returns Success, Message and Response = per room Id, UniqueId, Name, Number, RequestedNumber, " +
      "LevelName, Area (square feet) and Perimeter (feet); Area and Perimeter are in Revit internal units, " +
      "not metric.",
    schema: {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .describe("Room name (e.g. 'Server Room', 'Kitchen', 'Office')"),
            number: z
              .string()
              .optional()
              .describe("Requested room number (e.g. '101', 'A-01'); changed if already in use"),
            location: z
              .object({
                x: z.number().describe("X coordinate in mm (inside the enclosed area)"),
                y: z.number().describe("Y coordinate in mm (inside the enclosed area)"),
                z: z.number().describe("Z coordinate in mm; used to pick the nearest level when levelId is omitted"),
              })
              .describe("Point inside an enclosed area where the room is placed (mm)"),
            levelId: z
              .number()
              .optional()
              .describe("Element id of the level to place the room on. If omitted, the level nearest to location.z is used"),
            upperLimitId: z
              .number()
              .optional()
              .describe("Element id of the level used as the room's upper limit"),
            limitOffset: z
              .number()
              .optional()
              .describe("Offset from the upper limit in mm (only applied when greater than 0)"),
            baseOffset: z
              .number()
              .optional()
              .describe("Offset from the base level in mm"),
            department: z
              .string()
              .optional()
              .describe("Value for the room's Department parameter"),
            comments: z
              .string()
              .optional()
              .describe("Value for the room's Comments parameter"),
          })
        )
        .describe("Rooms to create"),
    },
  });
}
