using Unity.Play.Publisher.Editor;
using UnityEngine;

public class AttackState : BaseState
{
    private float moveTimer;
    private float losePlayerTimer;
    private float shotTimer;

    public override void Enter()
    {

    }

    public override void Exit()
    {

    }

    public override void Perform()
    {
        if (enemy.CanSeePlayer()) //player can be seen
        {
            //lock the lose player timer and increment the move and shot timers
            losePlayerTimer = 0;
            moveTimer += Time.deltaTime;
            shotTimer += Time.deltaTime;
            //enemy.transform.LookAt(enemy.Player.transform);

            // Keep enemy upright: look at player with Y locked to enemy's height
            Vector3 lookPos = enemy.Player.transform.position;
            lookPos.y = enemy.transform.position.y;
            enemy.transform.LookAt(lookPos);

            //if shot timer > firerate
            if (shotTimer > enemy.fireRate) 
            {
                Shoot();
                shotTimer = 0;
            }
            //move the enemy to a random position after a random time.
            if (moveTimer > Random.Range(3, 7))
            {
                enemy.Agent.SetDestination(enemy.transform.position + (Random.insideUnitSphere * 5f));
                moveTimer = 0;
            }
            enemy.LastKnownPos = enemy.Player.transform.position;
        }
        else //lost sight of player
        { 
            losePlayerTimer += Time.deltaTime;
            if (losePlayerTimer > 8) 
            {
                //change to search state 
                stateMachine.ChangeState(new SearchState());
            }
            
                
            
        }
    }

    public void Shoot() 
    {
        //store reference to gun barrel
        Transform gunBarrel = enemy.gunBarrel;
        //instantiate bullet at gun barrel position
        GameObject bullet = GameObject.Instantiate(Resources.Load("Prefabs/Bullet") as GameObject,gunBarrel.position,enemy.transform.rotation);
        //calculate direction to player
        Vector3 shootDirection = (enemy.Player.transform.position - gunBarrel.transform.position).normalized;
        //add force to rigid body
        bullet.GetComponent<Rigidbody>().linearVelocity = Quaternion.AngleAxis(Random.Range(-3f, 3f),Vector3.up) * shootDirection * 40;
        Debug.Log("Shoot");
        shotTimer = 0;
    }

}
