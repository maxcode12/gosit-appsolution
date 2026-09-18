import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import type { CaregiverInsights } from "@/features/caregivers/types";

/**
 * A caregiver's own figures.
 *
 * Two rules run through the whole component:
 *
 * - **An unknown is not a zero.** Every rate is null until there is something to divide
 *   by, and null renders as "—" with a reason rather than "0%". Telling a caregiver who
 *   has had no requests that they accept 0% of them would be a criticism of something
 *   that never happened.
 * - **Nothing here describes a family.** Repeat families is a count of accounts. No
 *   requester is named, and nothing is broken down in a way that would identify one.
 */
export function InsightsPanel({ insights }: { insights: CaregiverInsights }) {
  const hadRequests = insights.requestsReceived > 0;

  return (
    <div className="flex flex-col gap-8">
      <section>
        <h2 className="font-heading text-lg font-medium text-text-primary">
          Requests, last {insights.windowDays} days
        </h2>

        {hadRequests ? (
          <>
            <dl className="mt-4 grid gap-4 @sm:grid-cols-2 @2xl:grid-cols-4">
              <Stat
                label="Requests received"
                value={insights.requestsReceived.toString()}
                hint="Families who asked you to work."
              />
              <Stat
                label="You replied to"
                value={formatRate(insights.responseRate)}
                /*
                 * Three different sentences, because a rate below 100% has two very
                 * different causes. Requests still sitting in the queue are work to do;
                 * requests that ran out of time are a miss. Saying "you answered every
                 * request" beside "50%" — which is what this did when it looked only at
                 * the lapsed count — contradicts the number directly above it.
                 */
                hint={
                  insights.lapsedUnanswered > 0
                    ? `${insights.lapsedUnanswered} passed their start time with no answer.`
                    : insights.awaitingYourReply > 0
                      ? `${insights.awaitingYourReply} still waiting on you.`
                      : "You answered every request."
                }
                emphasis
              />
              <Stat
                label="Of those, accepted"
                value={formatRate(insights.acceptanceRate)}
                hint="Declining is not a mark against you."
              />
              <Stat
                label="Usually accepted within"
                value={formatDuration(insights.medianMinutesToAccept)}
                hint="Typical time from request to your yes."
              />
            </dl>

            <p className="mt-3 text-sm text-text-secondary">
              {insights.accepted} accepted · {insights.declined} declined
              {insights.cancelledByYou > 0
                ? ` · ${insights.cancelledByYou} cancelled by you after accepting`
                : ""}
            </p>
          </>
        ) : (
          <div className="mt-4">
            <EmptyState
              title="No requests yet"
              description={`Nobody has asked you to work in the last ${insights.windowDays} days. Once they do, how quickly you reply and how often you accept will show up here.`}
            />
          </div>
        )}
      </section>

      <section className="border-t border-border pt-8">
        <h2 className="font-heading text-lg font-medium text-text-primary">Work and families</h2>

        <dl className="mt-4 grid gap-4 @sm:grid-cols-2 @2xl:grid-cols-4">
          <Stat
            label={`Completed, last ${insights.windowDays} days`}
            value={insights.completedInWindow.toString()}
            hint="Bookings you finished."
          />
          <Stat
            label="Completed, all time"
            value={insights.completedLifetime.toString()}
            hint="Every booking you have finished."
          />
          <Stat
            label="Families who came back"
            value={insights.repeatFamilies.toString()}
            hint="Booked you more than once."
            emphasis
          />
          <Stat
            label="Rating"
            value={
              insights.averageRating === null
                ? "—"
                : `${insights.averageRating.toFixed(1)} ★`
            }
            hint={
              insights.reviewCount === 0
                ? "No reviews yet — new is not the same as unrated."
                : `From ${insights.reviewCount} ${insights.reviewCount === 1 ? "review" : "reviews"}.`
            }
          />
        </dl>
      </section>

      {/*
        The other half of the plan's promise. The flag stays because a deployment that
        has not started counting yet is indistinguishable from a caregiver nobody has
        seen, and those two deserve different sentences.
      */}
      {insights.searchPerformanceAvailable ? (
        <SearchSection insights={insights} />
      ) : (
        <section className="rounded-card border border-border bg-surface p-5">
          <h2 className="font-heading text-base font-semibold text-text-primary">
            Not measured yet: how you do in search
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-secondary">
            GoSit is not recording how often your profile appears in search or how often
            it is opened, so there is nothing here about it — the figures above are all
            from bookings. This is a gap in what we measure, not a sign that nobody is
            finding you.
          </p>
        </section>
      )}
    </div>
  );
}

/**
 * How often this caregiver was shown, and how often that turned into someone reading
 * the profile.
 *
 * Two figures and a rate, with the rate carrying the only actionable message on the
 * screen: appearances are decided by search ranking, but whether an appearance becomes a
 * view is decided by the photo, the rate and the opening line — the things a caregiver
 * can actually change.
 *
 * Deliberately says what is *not* counted. A caregiver comparing this to the number of
 * requests they received will otherwise wonder whether "appearances" includes people who
 * never scrolled to them, and a metric nobody can interpret is a metric that breeds
 * suspicion.
 */
function SearchSection({ insights }: { insights: CaregiverInsights }) {
  const seen = insights.searchAppearances > 0;

  return (
    <section>
      <h2 className="font-heading text-lg font-medium text-text-primary">
        In search, last {insights.windowDays} days
      </h2>

      {seen ? (
        <>
          <dl className="mt-4 grid gap-4 @sm:grid-cols-2 @2xl:grid-cols-3">
            <Stat
              label="Shown to families"
              value={insights.searchAppearances.toLocaleString("en-CA")}
              hint="Times you were on a page of results someone saw."
            />
            <Stat
              label="Profile opened"
              value={insights.profileViews.toLocaleString("en-CA")}
              hint="Times someone read your full profile."
            />
            <Stat
              label="Of those, opened"
              value={formatRate(insights.viewRate)}
              hint="What your photo, rate and first line are doing."
              emphasis
            />
          </dl>
        </>
      ) : (
        <p className="mt-4 rounded-card border border-dashed border-border-strong px-6 py-8 text-center text-sm text-text-secondary">
          You have not appeared in anyone&rsquo;s search results yet. Adding the services
          you offer and the days you are free is what puts you in them.
        </p>
      )}

      {/*
        Outside the branch on purpose. This is a statement about what GoSit records,
        which is true whether or not there is anything to show — and a caregiver with no
        appearances yet is exactly the person wondering what is being watched.
      */}
      <p className="mt-3 text-sm text-text-secondary">
        Counted per appearance, not per family — we do not record who searched.
      </p>
    </section>
  );
}

/**
 * The upsell, shown in place of figures when the plan does not include them.
 *
 * Says what the feature is and what it is not, because the plan screen's own notice
 * flags insights as the one feature still being built.
 */
export function InsightsLocked({ planHref }: { planHref: string }) {
  return (
    <div className="max-w-2xl">
      <EmptyState
        title="Insights are part of a paid plan"
        description="See how many requests you get, how quickly you reply, how often families come back, and how your rating is doing."
        action={
          <Link
            href={planHref}
            className="font-control text-sm text-brand-primary underline hover:text-text-primary"
          >
            See plans
          </Link>
        }
      />
    </div>
  );
}

function formatRate(rate: number | null): string {
  return rate === null ? "—" : `${Math.round(rate * 100)}%`;
}

/**
 * Minutes into something a person reads at a glance. "1,412 minutes" is technically the
 * answer and practically useless.
 */
function formatDuration(minutes: number | null): string {
  if (minutes === null) {
    return "—";
  }

  if (minutes < 60) {
    return `${minutes} min`;
  }

  const hours = Math.round(minutes / 60);

  if (hours < 48) {
    return `${hours} ${hours === 1 ? "hour" : "hours"}`;
  }

  const days = Math.round(hours / 24);

  return `${days} days`;
}

function Stat({
  label,
  value,
  hint,
  emphasis = false,
}: {
  label: string;
  value: string;
  hint: string;
  emphasis?: boolean;
}) {
  return (
    <div
      className={`rounded-card border p-5 ${
        emphasis ? "border-brand-soft-border bg-brand-soft" : "border-border bg-surface"
      }`}
    >
      <dt className="text-sm text-text-secondary">{label}</dt>
      <dd className="mt-1 font-heading text-3xl font-extrabold text-text-primary">{value}</dd>
      <p className="mt-1 text-sm text-text-secondary">{hint}</p>
    </div>
  );
}
