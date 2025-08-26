using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    // Message to display when the player can interact with this object
    public string promptMessage;


    //This function is called by the player when they interact with this object
    public void BaseInteract()
    {
        Interact();
    }

    protected virtual void Interact()
    {
        //we wont have code written in this function
        //this is a template function to be overridden by our subclasses
    }

}
