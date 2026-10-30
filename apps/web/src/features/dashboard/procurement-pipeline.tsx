import { getTranslations } from "next-intl/server";
import { dashboard } from "@/mocks/dashboard";

const stages = [
  { key: "requested", color: "bg-sand-500" },
  { key: "ordered", color: "bg-info-700" },
  { key: "delivered", color: "bg-primary" },
  { key: "invoiced", color: "bg-ok-600" },
] as const;

const max = Math.max(...Object.values(dashboard.pipeline));

export async function ProcurementPipeline() {
  const t = await getTranslations("dashboard.pipeline");

  return (
    <ul className="flex flex-col gap-5">
      {stages.map(({ key, color }) => (
        <li key={key} className="flex items-center gap-4">
          <span className="w-24 shrink-0 text-ui font-semibold">{t(key)}</span>
          <span className="h-2.5 flex-1 overflow-hidden rounded-[2px] bg-track">
            <i className={`block h-full ${color}`} style={{ width: `${(dashboard.pipeline[key] / max) * 100}%` }} />
          </span>
          <span className="num w-6 text-end text-ui font-semibold">{dashboard.pipeline[key]}</span>
        </li>
      ))}
    </ul>
  );
}
