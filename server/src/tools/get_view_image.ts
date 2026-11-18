import { z } from "zod";
import fs from "fs";
import os from "os";
import path from "path";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

const TOOL_NAME = "get_view_image";

/** Zelfde map als GetViewImageEventHandler.TempRoot in de commandset (%TEMP%\RevitMCP\view-images). */
const TEMP_ROOT = path.join(os.tmpdir(), "RevitMCP", "view-images");
/** Grotere afbeeldingen worden niet teruggestuurd; het model moet dan een kleinere size of jpg vragen. */
const MAX_IMAGE_BYTES = 4 * 1024 * 1024;
/** Achtergebleven mappen (bijv. na een crash) ouder dan dit worden bij het opstarten verwijderd. */
const ORPHAN_MAX_AGE_MS = 60 * 60 * 1000;

/**
 * Het pad komt uit het antwoord van Revit; lees en verwijder alleen iets als het bestand direct in
 * een eigen submap van TEMP_ROOT staat. realpath maakt korte 8.3-namen en hoofdletters gelijk.
 */
function resolveExportedFile(file: unknown): { file: string; folder: string } | null {
  if (typeof file !== "string" || file.length === 0) return null;
  try {
    const root = fs.realpathSync.native(TEMP_ROOT).toLowerCase();
    const real = fs.realpathSync.native(file);
    const folder = path.dirname(real);
    if (path.dirname(folder).toLowerCase() !== root) return null;
    return { file: real, folder };
  } catch {
    return null;
  }
}

/** Breedte en hoogte uit de PNG- of JPEG-header, zonder extra afhankelijkheden. */
function imageSize(data: Buffer, isPng: boolean): { width: number; height: number } | null {
  if (isPng) {
    return data.length >= 24 ? { width: data.readUInt32BE(16), height: data.readUInt32BE(20) } : null;
  }
  let i = 2; // na FF D8
  while (i + 9 < data.length) {
    if (data[i] !== 0xff) {
      i++;
      continue;
    }
    const marker = data[i + 1];
    if (marker === 0xff) {
      i++; // opvulbyte
      continue;
    }
    // SOF-markers (C0-CF behalve DHT C4, JPG C8 en DAC CC) bevatten de afmetingen
    if (marker >= 0xc0 && marker <= 0xcf && marker !== 0xc4 && marker !== 0xc8 && marker !== 0xcc) {
      return { height: data.readUInt16BE(i + 5), width: data.readUInt16BE(i + 7) };
    }
    i += 2 + data.readUInt16BE(i + 2);
  }
  return null;
}

/** Verwijdert achtergebleven exportmappen; best effort, blokkeert het opstarten niet. */
async function sweepOrphanFolders(): Promise<void> {
  try {
    const entries = await fs.promises.readdir(TEMP_ROOT, { withFileTypes: true });
    await Promise.all(
      entries
        .filter((entry) => entry.isDirectory())
        .map(async (entry) => {
          const dir = path.join(TEMP_ROOT, entry.name);
          try {
            const stat = await fs.promises.stat(dir);
            if (Date.now() - stat.birthtimeMs > ORPHAN_MAX_AGE_MS) {
              await fs.promises.rm(dir, { recursive: true, force: true });
            }
          } catch {
            // volgende keer opnieuw
          }
        })
    );
  } catch {
    // map bestaat nog niet
  }
}

function imageError(message: string, hint: string): CallToolResult {
  return {
    content: [{ type: "text", text: JSON.stringify({ tool: TOOL_NAME, error: { code: "image_error", message, hint } }) }],
    isError: true,
  };
}

async function buildImageResult(response: any): Promise<CallToolResult> {
  const exported = resolveExportedFile(response?.path);
  if (!exported) {
    return imageError(
      `Revit returned an unexpected image path: ${response?.path}`,
      "The MCP server and Revit must run on the same machine under the same Windows user. Tell the user."
    );
  }

  try {
    const data = await fs.promises.readFile(exported.file);
    const isPng = path.extname(exported.file).toLowerCase() === ".png";
    if (data.length > MAX_IMAGE_BYTES) {
      return imageError(
        `The image is ${(data.length / 1024 / 1024).toFixed(1)} MB, more than the ${MAX_IMAGE_BYTES / 1024 / 1024} MB limit.`,
        "Retry with a smaller size, with format 'jpg', or zoom in with elementIds/region."
      );
    }

    const { path: _path, ...info } = response;
    const dims = imageSize(data, isPng);
    const meta = {
      ...info,
      image: { width: dims?.width ?? null, height: dims?.height ?? null, format: isPng ? "png" : "jpg", bytes: data.length },
    };

    return {
      content: [
        { type: "text", text: JSON.stringify(meta) },
        { type: "image", data: data.toString("base64"), mimeType: isPng ? "image/png" : "image/jpeg" },
      ],
    };
  } finally {
    // Het bestand is alleen voor dit ene antwoord; direct opruimen
    await fs.promises.rm(exported.folder, { recursive: true, force: true }).catch(() => undefined);
  }
}

const elementId = z.union([z.number().int(), z.string().regex(/^\d+$/)]);

export function registerGetViewImageTool(server: McpServer) {
  void sweepOrphanFolders();

  defineRevitTool(server, {
    name: TOOL_NAME,
    title: "Get view image",
    kind: "read",
    description:
      "Render a Revit view to an image and return it to YOU for visual inspection: layout, geometry, " +
      "clashes, missing or overlapping annotations, or simply what the user is looking at. The image is " +
      "for your own analysis to decide next steps; it is not a deliverable for the user and no file is " +
      "kept, so do not tell the user an image was saved. Works for floor/ceiling/area plans, sections, " +
      "elevations, callout details, 3D views (also perspective), drafting views, legends and sheets. " +
      "Without viewId/viewName the active view is used. " +
      "Workflow: look at the whole view at the default size first. To read details (tags, dimensions, " +
      "small text, connections) zoom in with elementIds (crop to those elements plus paddingMm) or region " +
      "(coordinates taken from a previous response's 'region'), rather than raising size: the model " +
      "downsamples large images anyway. Zooming renders a temporary copy of the view that is rolled back; " +
      "the model, the original view and the user's screen do not change. Zoom works in plans, sections, " +
      "elevations, callouts and orthographic 3D views; not in perspective 3D, drafting views, legends or " +
      "sheets. " +
      "Response text: viewId, viewName, viewType, scale, zoomed, image width/height, and when known " +
      "'region': the area shown, in mm in the view's own 2D coordinates (x to the right, y up in the " +
      "image). regionMatchesModelXY=true means these equal model X/Y (unrotated plans), so they match " +
      "coordinates from ai_element_filter. Pixel to region is linear: x = minX + px/width*(maxX-minX), " +
      "y = maxY - py/height*(maxY-minY); approximate, the annotation crop adds a small margin. Unknown " +
      "view names return the list of available views. Exporting a large view can take tens of seconds.",
    schema: {
      viewId: z
        .number()
        .int()
        .optional()
        .describe("Element id of the view to render. Takes precedence over viewName."),
      viewName: z
        .string()
        .optional()
        .describe(
          "Exact view name (case-insensitive). If several views share the name (e.g. a floor plan and a " +
            "ceiling plan), the error lists them with their ids; then use viewId."
        ),
      size: z
        .number()
        .int()
        .min(200)
        .max(4000)
        .optional()
        .describe(
          "Long edge of the image in pixels, default 1568. 1568 is never downscaled by any Claude model. Up " +
            "to ~2576 adds detail on current Claude models (Opus 4.7+, Sonnet 5+, Fable) at roughly 3x the " +
            "image tokens; larger only helps other models. Prefer zooming over a larger size."
        ),
      format: z
        .enum(["auto", "png", "jpg"])
        .optional()
        .describe(
          "auto (default): PNG for hidden-line/wireframe views, JPG for shaded/realistic views. png: sharpest " +
            "line work. jpg: smallest files."
        ),
      elementIds: z
        .array(elementId)
        .optional()
        .describe(
          "Zoom to these elements: the image is cropped to their combined bounding box plus paddingMm. Ids " +
            "come from ai_element_filter, get_selected_elements or get_current_view_elements. Ids that do " +
            "not exist are reported in missingElementIds. Do not combine with region."
        ),
      paddingMm: z
        .number()
        .min(0)
        .max(100000)
        .optional()
        .describe("Margin around elementIds in mm (model size), default 1000."),
      region: z
        .object({
          minX: z.number(),
          minY: z.number(),
          maxX: z.number(),
          maxY: z.number(),
        })
        .optional()
        .describe(
          "Zoom to this rectangle, in mm in the view's 2D coordinates as returned in 'region' by a previous " +
            "call on the same view (equal to model X/Y when regionMatchesModelXY was true). Do not combine " +
            "with elementIds."
        ),
    },
    buildResult: (response) => buildImageResult(response),
  });
}
