// Mercosul plate: white plate, blue band. Real-world colors, so they don't follow the light/dark theme.
export function Plate({ value }: { value: string }) {
  return (
    <span className="inline-flex w-22 flex-col overflow-hidden rounded-sm border-[1.5px] border-plate-foreground bg-plate text-center align-middle">
      <span aria-hidden className="bg-plate-band text-[7px] leading-2.5 font-semibold tracking-[0.12em] text-white">BRASIL</span>
      <span className="font-mono text-[13px] leading-5 font-semibold tracking-wider text-plate-foreground">{value}</span>
    </span>
  )
}
