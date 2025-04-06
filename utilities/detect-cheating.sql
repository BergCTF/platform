-- Detect cheating players by looking for invalid flag submissions that contain valid flags of other players
select
	i."ChallengeName" as "Challenge",
	i."StartedAt" as "InstanceStartedAt",
	i."TerminatedAt" as "InstanceTerminated",
	s."SubmittedAt" as "SubmittedAt",
	i."PlayerId" as "InstancePlayerId",
	s."PlayerId" as "SubmitterPlayerId",
from "Submissions" as s join "Instances" as i on s."Value" = i."DynamicFlag" where s."PlayerId" != i."PlayerId";

