using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    //Add or remove an interactionevent component to this object
    public bool useEvents;

    // Message to display when the player can interact with this object
    [SerializeField]public string promptMessage;

    public virtual string OnLook()
    {
        return promptMessage;
    }

    //This function is called by the player when they interact with this object
    public void BaseInteract()
    {
        if(useEvents)
            GetComponent<InteractionEvent>().onInteract.Invoke();
        Interact();
    }

    protected virtual void Interact()
    {
        //we wont have code written in this function
        //this is a template function to be overridden by our subclasses
    }

}
