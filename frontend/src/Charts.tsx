import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { Comparison } from "./api";
const money = (n: number) =>
  new Intl.NumberFormat("he-IL", {
    style: "currency",
    currency: "ILS",
    maximumFractionDigits: 0,
    minimumFractionDigits: 0,
  }).format(n);
export function SalaryDonut({
  components,
}: {
  components: { name: string; value: number }[];
}) {
  return (
    <ResponsiveContainer width="100%" height="100%">
      <PieChart>
        <Pie
          isAnimationActive={false}
          data={components}
          dataKey="value"
          innerRadius="70%"
          outerRadius="95%"
          strokeWidth={0}
          startAngle={90}
          endAngle={-270}
        >
          {components.map((c, i) => (
            <Cell key={c.name} fill={["#168a81", "#82c6b8", "#d6eae5"][i]} />
          ))}
        </Pie>
        <Tooltip formatter={(value) => money(Number(value))} />
      </PieChart>
    </ResponsiveContainer>
  );
}
export function WaterfallChart({
  points,
}: {
  points: Comparison["waterfall"];
}) {
  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart
        data={points}
        margin={{ top: 18, left: 0, right: 10, bottom: 15 }}
      >
        <CartesianGrid
          vertical={false}
          stroke="#e8eeef"
          strokeDasharray="3 4"
        />
        <XAxis
          dataKey="name"
          tick={<WaterfallTick />}
          axisLine={false}
          tickLine={false}
          interval={0}
        />
        <YAxis
          width={52}
          tick={{ fontSize: 10, fill: "#617682" }}
          tickFormatter={(v) => `${Number(v) / 1000}k`}
          axisLine={false}
          tickLine={false}
        />
        <Tooltip
          formatter={(_, __, item) => [
            money(Number(item.payload.change)),
            "השפעה על הנטו",
          ]}
          contentStyle={{
            borderRadius: 12,
            border: "1px solid #e1e9e9",
            direction: "rtl",
          }}
        />
        <Bar
          dataKey="base"
          stackId="net"
          fill="transparent"
          tooltipType="none"
          isAnimationActive={false}
        />
        <Bar
          dataKey="amount"
          stackId="net"
          radius={[5, 5, 0, 0]}
          maxBarSize={46}
          isAnimationActive={false}
        >
          {points.map((p, i) => (
            <Cell
              key={i}
              fill={p.total ? "#19394e" : p.change >= 0 ? "#168a81" : "#dc8d74"}
            />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

function WaterfallTick({
  x = 0,
  y = 0,
  payload,
}: {
  x?: number | string;
  y?: number | string;
  payload?: { value: string };
}) {
  return (
    <g transform={`translate(${x},${y})`}>
      <text
        fill="#526573"
        fontSize={11}
        fontFamily="Heebo Variable"
        textAnchor="middle"
      >
        {payload?.value.split(" ").map((word, i) => (
          <tspan x={0} dy={i === 0 ? 14 : 14} key={i}>
            {word}
          </tspan>
        ))}
      </text>
    </g>
  );
}
