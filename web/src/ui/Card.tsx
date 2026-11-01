import type { HTMLAttributes, ReactNode } from 'react'

type CardProps = HTMLAttributes<HTMLElement> & {
  /** The element to render: a plain box, or a section with its own heading. */
  as?: 'div' | 'section' | 'article' | 'li'
  /** Inner spacing; off for cards made of rows that reach the edges (lists, tables). */
  padded?: boolean
  children: ReactNode
}

/** A white (dark: raised) rounded box: the building block of every screen. */
export function Card({
  as: Element = 'div',
  padded = true,
  className,
  children,
  ...rest
}: CardProps) {
  const classes = ['ui-card', padded ? 'ui-card-padded' : null, className].filter(Boolean).join(' ')
  return (
    <Element className={classes} {...rest}>
      {children}
    </Element>
  )
}
