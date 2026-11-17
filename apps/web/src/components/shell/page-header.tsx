import type { ReactNode } from "react";

type PageHeaderProps = {
  title: string;
  actions?: ReactNode;
};

export function PageHeader({ title, actions }: PageHeaderProps) {
  return (
    <div className="flex flex-wrap items-end gap-4">
      <h1 className="page-title">{title}</h1>
      {actions && <div className="ms-auto flex gap-2">{actions}</div>}
    </div>
  );
}
