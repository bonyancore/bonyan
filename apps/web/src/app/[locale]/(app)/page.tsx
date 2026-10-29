import { getFormatter, getTranslations } from "next-intl/server";
import { Money } from "@/components/i18n/money";
import { Reveal } from "@/components/motion/reveal";
import { PageHeader } from "@/components/shell/page-header";
import { Card, Overline } from "@/components/ui/card";
import { Chip } from "@/components/ui/chip";
import { CashFlowChart } from "@/features/dashboard/cash-flow-chart";
import { KpiTile } from "@/features/dashboard/kpi-tile";
import { NeedsYou } from "@/features/dashboard/needs-you";
import { PackageProgress } from "@/features/dashboard/package-progress";
import { ProcurementPipeline } from "@/features/dashboard/procurement-pipeline";
import { getDashboard } from "@/features/dashboard/queries";

export default async function HomePage() {
  const [nav, t, format, data] = await Promise.all([
    getTranslations("nav"),
    getTranslations("dashboard"),
    getFormatter(),
    getDashboard(),
  ]);
  const { currency, contract, progress, cash, approvals } = data;
  const points = Math.round((progress.actual - progress.planned) * 100);

  return (
    <>
      <PageHeader title={nav("home")} />

      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <KpiTile
          order={0}
          label={t("kpi.contract")}
          value={<Money value={contract.value} currency={currency} compact />}
          note={
            <Chip tone="neutral" marker={false}>
              {t("note.variations", {
                percent: format.number(contract.variations, "percent"),
              })}
            </Chip>
          }
          trend={contract.trend}
        />
        <KpiTile
          order={1}
          label={t("kpi.progress")}
          value={
            <span className="num">
              {format.number(progress.actual, "percent")}
            </span>
          }
          note={<Chip tone="ok">{t("note.ahead", { points })}</Chip>}
          trend={progress.trend}
        />
        <KpiTile
          order={2}
          label={t("kpi.cash")}
          value={<Money value={cash.value} currency={currency} compact />}
          note={
            <Chip tone="neutral" marker={false}>
              {t("note.runway", { weeks: cash.weeks })}
            </Chip>
          }
          trend={cash.trend}
        />
        <KpiTile
          order={3}
          label={t("kpi.approvals")}
          value={<span className="num">{approvals.open}</span>}
          note={
            <Chip tone="danger">
              {t("note.overdue", { count: approvals.overdue })}
            </Chip>
          }
          trend={approvals.trend}
        />
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Reveal delay={0.2} className="xl:col-span-2">
          <Card className="flex h-full flex-col gap-5 p-5">
            <div className="flex items-center gap-4">
              <div>
                <Overline className="mb-0.5">{t("cashFlow.title")}</Overline>
                <p className="text-caption text-ink-600">
                  {t("cashFlow.unit")}
                </p>
              </div>
              <ul className="ms-auto flex gap-4 text-caption text-ink-700">
                <li className="flex items-center gap-2">
                  <i
                    aria-hidden
                    className="h-0 w-4 border-t-2 border-dashed border-ink-300"
                  />
                  {t("cashFlow.planned")}
                </li>
                <li className="flex items-center gap-2">
                  <i
                    aria-hidden
                    className="h-0.5 w-4 rounded-full bg-primary"
                  />
                  {t("cashFlow.actual")}
                </li>
              </ul>
            </div>
            <CashFlowChart points={data.months} />
          </Card>
        </Reveal>
        <Reveal delay={0.25}>
          <NeedsYou />
        </Reveal>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Reveal delay={0.3}>
          <Card className="flex h-full flex-col gap-5 p-5">
            <Overline className="mb-0">{t("packages.title")}</Overline>
            <PackageProgress
              items={data.packages}
              plannedLabel={t("packages.planned")}
            />
          </Card>
        </Reveal>
        <Reveal delay={0.35}>
          <Card className="flex h-full flex-col gap-5 p-5">
            <Overline className="mb-0">{t("pipeline.title")}</Overline>
            <ProcurementPipeline />
          </Card>
        </Reveal>
      </div>
    </>
  );
}
