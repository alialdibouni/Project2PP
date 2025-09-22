using UnityEngine;

public class SearchState : BaseState
{
    private float searchTimer;
    private float moveTimer;

    public override void Enter()
    {
        enemy.Agent.SetDestination(enemy.LastKnownPos);
    }

    public override void Perform()
    {
        if(enemy.CanSeePlayer()) //can see player
        {
            stateMachine.ChangeState(new ChaseState());
        }
        if(enemy.Agent.remainingDistance < enemy.Agent.stoppingDistance) //reached last known position
        {
            searchTimer += Time.deltaTime;
            moveTimer += Time.deltaTime;
            //move the enemy to a random position after a random time.
            if (moveTimer > Random.Range(3, 5))
            {
                enemy.Agent.SetDestination(enemy.transform.position + (Random.insideUnitSphere * 10f));
                moveTimer = 0;
            }
            if (searchTimer > Random.Range(5f, 12f)) //searched between 5 and 12 seconds
            {
                stateMachine.ChangeState(new PatrolState());
            }
        }
    }

    public override void Exit()
    {

    }
}
