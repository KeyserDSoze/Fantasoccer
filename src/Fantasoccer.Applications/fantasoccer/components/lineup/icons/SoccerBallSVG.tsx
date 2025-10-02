import React from 'react'

interface SoccerBallSVGProps {
  primaryColor?: string
  secondaryColor?: string
  size?: number
  [key: string]: any // per altri props SVG
}

// Pallone da calcio riutilizzabile con colori personalizzabili
export function SoccerBallSVG({
  primaryColor = '#ffffff',
  secondaryColor = '#000000',
  size = 16,
  ...props
}: SoccerBallSVGProps) {
  const CX = 50, CY = 50;
  const outerR = 48;    // raggio della palla "base"
  const seamW = 2.2;    // spessore cuciture

  const poly = (cx: number, cy: number, r: number, sides: number, rotDeg = 0) => {
    const rot = (Math.PI / 180) * rotDeg;
    const pts = Array.from({ length: sides }, (_, i) => {
      const a = rot + (i * 2 * Math.PI) / sides - Math.PI / 2;
      const x = cx + r * Math.cos(a);
      const y = cy + r * Math.sin(a);
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    }).join(' ');
    return pts;
  };

  const centerPentR = 11.5;
  const ringHexR = 25.5;
  const hexR = 12.3;
  const outerPentR = 40.5;
  const outerPentSize = 10.5;

  const hexCenters = Array.from({ length: 5 }, (_, i) => {
    const ang = (-90 + i * 72) * Math.PI / 180;
    return [CX + ringHexR * Math.cos(ang), CY + ringHexR * Math.sin(ang)] as const;
  });

  const outerPentCenters = Array.from({ length: 5 }, (_, i) => {
    const ang = (-90 + 36 + i * 72) * Math.PI / 180;
    return [CX + outerPentR * Math.cos(ang), CY + outerPentR * Math.sin(ang)] as const;
  });

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 100 100"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
      focusable="false"
      role="img"
      aria-label="Soccer ball icon"
      {...props}
    >
      <defs>
        <clipPath id="ballClip">
          <circle cx={CX} cy={CY} r={outerR} />
        </clipPath>
      </defs>

      {/* cerchio base */}
      <circle
        cx={CX}
        cy={CY}
        r={outerR}
        fill={primaryColor}
      />

      <g clipPath="url(#ballClip)">
        {/* pentagono centrale */}
        <polygon
          points={poly(CX, CY, centerPentR, 5, 0)}
          fill={secondaryColor}
          stroke={secondaryColor}
          strokeWidth={seamW}
        />

        {/* esagoni */}
        {hexCenters.map(([x, y], idx) => (
          <polygon
            key={`hex-${idx}`}
            points={poly(x, y, hexR, 6, 30)}
            fill={primaryColor}
            stroke={secondaryColor}
            strokeWidth={seamW}
          />
        ))}

        {/* pentagoni esterni */}
        {outerPentCenters.map(([x, y], idx) => (
          <polygon
            key={`opent-${idx}`}
            points={poly(x, y, outerPentSize, 5, 18)}
            fill={secondaryColor}
            stroke={secondaryColor}
            strokeWidth={seamW}
          />
        ))}

        {/* highlights */}
        <path d="M76 18c5 3 9 6 12 10" stroke={primaryColor} strokeWidth={seamW} fill="none" />
        <path d="M18 78c3 4 6 7 10 9" stroke={primaryColor} strokeWidth={seamW} fill="none" />
      </g>

      {/* ✅ contorno esterno visibile */}
      <circle
        cx={CX}
        cy={CY}
        r={outerR + seamW / 2}   // leggermente più grande
        fill="none"
        stroke={secondaryColor == '#ffffff' ? primaryColor : secondaryColor}
        strokeWidth={seamW * 1.5}
      />
    </svg>
  );
}

export default SoccerBallSVG