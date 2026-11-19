import { useId } from "react";

export function CarArt({
  electric = false,
  hero = false,
}: {
  electric?: boolean;
  hero?: boolean;
}) {
  const id = useId().replaceAll(":", "");
  return (
    <svg
      viewBox="0 0 420 180"
      className={`car-art ${hero ? "hero-car" : ""}`}
      role="img"
      aria-label="איור רכב להמחשה"
    >
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="0.8" y2="1">
          <stop stopColor={electric ? "#c9e8e3" : "#dce5ec"} />
          <stop offset=".5" stopColor={electric ? "#6baba3" : "#9eafbf"} />
          <stop offset="1" stopColor={electric ? "#285f61" : "#526c82"} />
        </linearGradient>
      </defs>
      <ellipse
        cx="216"
        cy="155"
        rx="160"
        ry="12"
        fill="#081e32"
        opacity=".12"
      />
      <path
        d="M45 119 62 100 110 91 149 54 Q158 47 182 47 L258 50 Q281 52 306 86 L353 97 Q373 104 376 124 L373 140 49 140Z"
        fill={`url(#${id})`}
        stroke={electric ? "#548d88" : "#71899e"}
        strokeWidth="1.4"
      />
      <path d="M126 90 160 56 200 55 199 88Z" fill="#173143" />
      <path d="m209 55 46 3q17 1 40 30l-87 0Z" fill="#18394b" />
      <path d="m163 58 32-1-30 31-34 0Z" fill="#b9e0e6" opacity=".25" />
      <path d="m217 58 34 3 25 27-35 0Z" fill="#b9e0e6" opacity=".17" />
      <path
        d="m53 113 53-9m206-8 44 9M113 101l-5 31m97-36-2 37m99-36 8 33"
        fill="none"
        stroke="#38596b"
        opacity=".5"
      />
      <path d="M53 118h25l-10 10H49m298-18h18l7 12h-21" fill="#f4f6de" />
      <path d="M113 139h190" stroke="#365567" strokeWidth="5" />
      <path
        d="M174 100h16m64 0h16"
        stroke="#deeef0"
        strokeWidth="3"
        strokeLinecap="round"
      />
      {[106, 315].map((x) => (
        <g key={x}>
          <circle cx={x} cy="138" r="26" fill="#1d2a39" />
          <circle cx={x} cy="138" r="17" fill="#b6c6cd" />
          <circle cx={x} cy="138" r="12" fill="#597080" />
          <path
            d={`M${x} 126v24m-12-12h24m-20-8 16 16m-16 0 16-16`}
            stroke="#d8e3e7"
            strokeWidth="3"
          />
          <circle cx={x} cy="138" r="4" fill="#bfccd3" />
        </g>
      ))}
      <path d="M294 84h16l6 10h-19" fill="#527d8d" />
      {electric && (
        <path d="m335 115-8 10h6l-4 9 12-13h-7l5-6Z" fill="#bff8d5" />
      )}
    </svg>
  );
}
