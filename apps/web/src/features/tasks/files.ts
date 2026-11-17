import type { FileKind } from "./types";

export const kindOf = (file: File): FileKind =>
  file.type.startsWith("image/") ? "image" : /\.(xlsx?|csv)$/i.test(file.name) ? "sheet" : "file";
