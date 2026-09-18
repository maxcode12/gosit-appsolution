"use client";

import "@livekit/components-styles";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import {
  ControlBar,
  GridLayout,
  LiveKitRoom,
  ParticipantTile,
  RoomAudioRenderer,
  useTracks,
} from "@livekit/components-react";
import { Track } from "livekit-client";
import { Button, buttonClassName } from "@/components/ui/button";
import Link from "next/link";
import { joinInterviewAction } from "@/features/interviews/actions";
import { InterviewChat } from "@/features/interviews/components/interview-chat";
import { formatSlot, type Interview, type JoinTicket } from "@/features/interviews/types";
import type { Message } from "@/features/messaging/types";

/**
 * The call.
 *
 * Three stages: agree to the recording, get a token, talk. The token is fetched only after
 * consent is given and only on this screen, so a page load never carries a way into a room.
 *
 * What the controls offer is deliberate: microphone, camera, leave. No screen sharing —
 * a shared screen is the easiest way to hold a phone number up to a camera — and no
 * LiveKit chat, because chat goes into the pair's GoSit conversation where it is kept
 * and moderated like every other message.
 */
export function InterviewRoom({
  interview,
  otherName,
  initialMessages,
  backHref,
}: {
  interview: Interview;
  otherName: string;
  initialMessages: Message[];
  backHref: string;
}) {
  const router = useRouter();
  const [ticket, setTicket] = useState<JoinTicket | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [consented, setConsented] = useState(false);
  const [isJoining, startJoining] = useTransition();

  function join() {
    startJoining(async () => {
      const result = await joinInterviewAction(interview.id, consented);

      if (result.error || !result.ticket) {
        setError(result.error ?? "We could not start the interview.");
        return;
      }

      setError(null);
      setTicket(result.ticket);
    });
  }

  if (!ticket) {
    return (
      <div className="surface-card max-w-xl p-6">
        <h2 className="font-heading text-lg font-semibold text-text-primary">
          Before you join
        </h2>
        <p className="mt-2 text-sm text-text-secondary">
          {formatSlot(interview)} with {otherName}.
        </p>

        <div className="mt-4 rounded-control bg-surface-muted p-4 text-sm text-text-primary">
          <p className="font-medium">This interview is recorded.</p>
          <p className="mt-1 text-text-secondary">
            Video, audio and the chat are kept for 90 days so there is a record if either of
            you raises a concern. Only GoSit&apos;s Trust &amp; Safety staff can watch it, every
            view is logged, and it is never shared with the other person. Recording is how
            interviews work here — you cannot join without it.
          </p>
        </div>

        <label className="mt-4 flex items-start gap-3 text-sm text-text-primary">
          <input
            type="checkbox"
            checked={consented}
            onChange={(event) => setConsented(event.target.checked)}
            className="mt-0.5 size-4"
          />
          <span>I understand this interview is recorded, and I agree to join.</span>
        </label>

        {error ? (
          <p role="alert" className="mt-3 text-sm text-danger-accent">
            {error}
          </p>
        ) : null}

        <div className="mt-5 flex flex-wrap gap-2">
          <Button type="button" onClick={join} disabled={!consented || isJoining}>
            {isJoining ? "Starting…" : "Join the call"}
          </Button>
          <Link href={backHref} className={buttonClassName("ghost")}>
            Not now
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <LiveKitRoom
        serverUrl={ticket.serverUrl}
        token={ticket.token}
        connect
        video
        audio
        // Leaving — the control bar's button, a closed tab coming back, a dropped
        // connection that does not recover — lands on the interview page, not a blank room.
        onDisconnected={() => router.push(backHref)}
        onError={(roomError) => setError(roomError.message)}
        data-lk-theme="default"
        className="flex min-h-[28rem] flex-col overflow-hidden rounded-card border border-border bg-brand-deep"
      >
        <Stage />
        <RoomAudioRenderer />
        <ControlBar
          variation="minimal"
          controls={{ microphone: true, camera: true, screenShare: false, chat: false, leave: true }}
        />

        {error ? (
          <p role="alert" className="px-4 pb-3 text-sm text-danger-accent">
            {error}
          </p>
        ) : null}

        <InterviewChat conversationId={interview.conversationId} initialMessages={initialMessages} otherName={otherName} />
      </LiveKitRoom>
    </div>
  );
}

/** Both cameras, side by side. A placeholder tile stands in for a camera that is off. */
function Stage() {
  const tracks = useTracks(
    [
      { source: Track.Source.Camera, withPlaceholder: true },
    ],
    { onlySubscribed: false },
  );

  return (
    <GridLayout tracks={tracks} className="min-h-[22rem] flex-1">
      <ParticipantTile />
    </GridLayout>
  );
}
