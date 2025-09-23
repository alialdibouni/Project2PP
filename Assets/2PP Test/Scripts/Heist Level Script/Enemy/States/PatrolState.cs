using UnityEngine;

public class PatrolState : BaseState
{
    public int waypointIndex; // track which waypoint we are currently targetting
    public float waitTimer;   // how long we have waited at a waypoint

    public override void Enter()
    {
        // Ensure we have a valid path and waypoints
        if (enemy.path == null || enemy.path.waypoints == null || enemy.path.waypoints.Count == 0)
        {
            Debug.LogWarning($"[{nameof(PatrolState)}] Enemy '{enemy.name}' has no path/waypoints assigned.");
            return;
        }

        // Make sure the agent can move
        if (enemy.Agent != null)
        {
            enemy.Agent.isStopped = false;
        }

        // Clamp index and pick a sensible starting waypoint (nearest), then set destination
        waypointIndex = Mathf.Clamp(waypointIndex, 0, enemy.path.waypoints.Count - 1);

        int nearest = 0;
        float minSqr = float.MaxValue;
        for (int i = 0; i < enemy.path.waypoints.Count; i++)
        {
            var wp = enemy.path.waypoints[i];
            if (wp == null) continue;
            float sqr = (enemy.transform.position - wp.position).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr = sqr;
                nearest = i;
            }
        }

        waypointIndex = nearest;

        // If we're basically on top of the nearest, move to the next one to keep moving
        if (Vector3.Distance(enemy.transform.position, enemy.path.waypoints[waypointIndex].position) < 0.5f)
        {
            waypointIndex = (waypointIndex + 1) % enemy.path.waypoints.Count;
        }

        enemy.Agent.SetDestination(enemy.path.waypoints[waypointIndex].position);
        waitTimer = 0f;
    }

    public override void Perform()
    {
        PatrolCycle();

        // Only chase if the player is inside the guard area AND visible
        if (enemy.IsPlayerInGuardArea && enemy.CanSeePlayer())
        {
            stateMachine.ChangeState(new ChaseState());
        }
    }

    public override void Exit()
    {
        // Optional: reset wait timer when leaving patrol
        waitTimer = 0f;
    }

    private void PatrolCycle()
    {
        if (enemy.path == null || enemy.path.waypoints == null || enemy.path.waypoints.Count == 0) return;

        // Robust "arrived" check: not pending and within stoppingDistance (+ small tolerance)
        if (!enemy.Agent.pathPending &&
            enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance + 0.1f)
        {
            waitTimer += Time.deltaTime;

            // Wait briefly at each waypoint
            if (waitTimer > 3f)
            {
                waypointIndex = (waypointIndex + 1) % enemy.path.waypoints.Count;

                // Skip null waypoints defensively
                var nextWp = enemy.path.waypoints[waypointIndex];
                if (nextWp != null)
                {
                    enemy.Agent.isStopped = false;
                    enemy.Agent.SetDestination(nextWp.position);
                }

                waitTimer = 0f;
            }
        }
    }
}
