"use client";

import { useOptimistic, useTransition } from "react";
import { HeartIcon } from "@/components/ui/icons";
import { addFavoriteAction, removeFavoriteAction } from "@/features/favorites/actions";

/**
 * Saves or unsaves a caregiver.
 *
 * Optimistic, because the round trip is not worth a visible delay on a toggle. If the
 * server refuses, the optimistic state is discarded on the next render and the message
 * is surfaced rather than swallowed — a save that silently did not happen is worse than
 * a slow one.
 */
export function FavouriteButton({
  caregiverProfileId,
  caregiverName,
  isFavourite,
}: {
  caregiverProfileId: string;
  caregiverName: string;
  isFavourite: boolean;
}) {
  const [isPending, startTransition] = useTransition();
  const [optimistic, setOptimistic] = useOptimistic(isFavourite);

  function toggle() {
    startTransition(async () => {
      setOptimistic(!optimistic);

      const result = optimistic
        ? await removeFavoriteAction(caregiverProfileId)
        : await addFavoriteAction(caregiverProfileId);

      if (result.error) {
        // Surfaced rather than swallowed. The optimistic value resets when the
        // revalidated server state arrives.
        window.alert(result.error);
      }
    });
  }

  return (
    <button
      type="button"
      onClick={toggle}
      disabled={isPending}
      aria-pressed={optimistic}
      aria-label={optimistic ? `Remove ${caregiverName} from saved` : `Save ${caregiverName}`}
      className={`grid size-10 place-items-center rounded-pill border transition-colors disabled:opacity-60 ${
        optimistic
          ? "border-brand-secondary bg-brand-secondary/10 text-brand-secondary"
          : "border-border text-text-secondary hover:border-brand-secondary hover:text-brand-secondary"
      }`}
    >
      <HeartIcon filled={optimistic} />
    </button>
  );
}
