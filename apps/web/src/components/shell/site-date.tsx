import { connection } from "next/server";
import { isoDate } from "@/i18n/dates";

export async function SiteDate() {
  await connection();
  return <span className="num text-sm font-semibold">{isoDate(new Date())}</span>;
}
