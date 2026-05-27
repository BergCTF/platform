-- Detect cheating players by looking for invalid flag submissions that contain valid flags of other players that are not on the same team
SELECT
    i."ChallengeName" AS "Challenge",
    i."StartedAt" AS "InstanceStartedAt",
    i."TerminatedAt" AS "InstanceTerminated",
    s."SubmittedAt" AS "SubmittedAt",
    i."PlayerId" AS "InstancePlayerId",
    ip."Name" AS "InstancePlayerName",
    s."PlayerId" AS "SubmitterPlayerId",
    sp."Name" AS "SubmitterPlayerName"
FROM "Submissions" AS s
JOIN "Instances" AS i ON s."Value" = i."DynamicFlag"
JOIN "Players" AS ip ON i."PlayerId" = ip."Id"
JOIN "Players" AS sp ON s."PlayerId" = sp."Id"
WHERE s."PlayerId" != i."PlayerId" AND (ip."TeamId" IS NULL OR sp."TeamId" IS NULL OR ip."TeamId" != sp."TeamId");