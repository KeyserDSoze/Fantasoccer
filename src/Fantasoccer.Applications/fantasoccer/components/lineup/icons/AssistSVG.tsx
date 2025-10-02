import React from 'react'

interface AssistSVGProps {
  color?: string
  size?: number
  strokeWidth?: number
  [key: string]: any // per altri props SVG
}

export function AssistSVG({
  color = "#000000",
  size = 19,
  strokeWidth = 12, // facoltativo: puoi regolare lo spessore del tratto
  ...props
}: AssistSVGProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 200 200"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label="Assist icon"
      {...props}
    >
      {/* Contorno scarpa */}
      <path
        d="M18 122c0-38 18-58 48-64l64-14c5 10 17 17 31 17h31v96H18v-35Z"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        strokeLinejoin="round"
      />

      {/* Foro/occhiello superiore */}
      <circle
        cx="144"
        cy="56"
        r="14"
        stroke={color}
        strokeWidth={strokeWidth}
      />

      {/* Suola */}
      <line
        x1="18"
        y1="150"
        x2="192"
        y2="150"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />

      {/* Tre strisce diagonali */}
      <line
        x1="75"
        y1="68"
        x2="105"
        y2="140"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />
      <line
        x1="96"
        y1="62"
        x2="126"
        y2="140"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />
      <line
        x1="117"
        y1="58"
        x2="147"
        y2="140"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />

      {/* “Tasselli” suola */}
      <rect
        x="26"
        y="150"
        width="34"
        height="26"
        rx="4"
        stroke={color}
        strokeWidth={strokeWidth}
      />
      <rect
        x="78"
        y="150"
        width="34"
        height="26"
        rx="4"
        stroke={color}
        strokeWidth={strokeWidth}
      />
      <rect
        x="130"
        y="150"
        width="34"
        height="26"
        rx="4"
        stroke={color}
        strokeWidth={strokeWidth}
      />
    </svg>
  );
}

export default AssistSVG
