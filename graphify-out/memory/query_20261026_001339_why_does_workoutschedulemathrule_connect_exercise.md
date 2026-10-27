---
type: "query"
date: "2026-10-26T00:13:39.078396+00:00"
question: "Why does WorkoutScheduleMathRule connect Exercise Selection & Candidate Filtering to WorkoutOverview & WorkoutSessionAction?"
contributor: "graphify"
source_nodes: ["WorkoutScheduleMathRule", "WorkoutOverviewRule", "WorkoutSessionActionRule"]
---

# Q: Why does WorkoutScheduleMathRule connect Exercise Selection & Candidate Filtering to WorkoutOverview & WorkoutSessionAction?

## Answer

WorkoutScheduleMathRule.IsSupportedDate() validates calendar dates within the planning horizon and is invoked by both WorkoutOverviewRule.IsValidReadinessAdjustmentRequest() and WorkoutSessionActionRule.IsValidSessionLog() in Community 18, creating a cross-community bridge between plan generation and session execution.

## Source Nodes

- WorkoutScheduleMathRule
- WorkoutOverviewRule
- WorkoutSessionActionRule