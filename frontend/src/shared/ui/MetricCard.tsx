import type { ReactNode } from 'react';

interface MetricCardProps {
  value: ReactNode;
  label: string;
}

export function MetricCard({ value, label }: MetricCardProps) {
  return (
    <div className="metric-card">
      <strong>{value}</strong>
      <span>{label}</span>
    </div>
  );
}
