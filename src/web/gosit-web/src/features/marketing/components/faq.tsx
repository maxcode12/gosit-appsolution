import { PlusIcon } from "@/components/ui/marketing-icons";

export type FaqItem = { question: string; answer: string };

/**
 * Questions and answers as native disclosure elements: keyboard and screen-reader
 * support, and the answer is still in the page for search engines, with no script.
 */
export function Faq({ items }: { items: FaqItem[] }) {
  return (
    <div className="divide-y divide-border overflow-hidden rounded-3xl border border-border bg-surface shadow-sm">
      {items.map((item) => (
        <details key={item.question} className="group">
          <summary className="flex cursor-pointer items-center justify-between gap-6 px-6 py-5 font-heading text-lg font-semibold text-text-primary transition-colors hover:bg-surface-muted sm:px-8 sm:py-6">
            {item.question}
            <span className="grid size-8 shrink-0 place-items-center rounded-full bg-brand-soft text-brand-primary transition-transform duration-200 group-open:rotate-45">
              <PlusIcon className="size-4" />
            </span>
          </summary>
          <p className="px-6 pb-6 pr-16 text-base leading-relaxed text-text-secondary sm:px-8 sm:pb-7">
            {item.answer}
          </p>
        </details>
      ))}
    </div>
  );
}
