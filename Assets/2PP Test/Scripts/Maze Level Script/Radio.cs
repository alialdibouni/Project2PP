using System.Collections.Generic;
using UnityEngine;

public class Radio : MonoBehaviour
{
    public List<AudioClip> radioSongs; // List of audio clips for radio stations
    public AudioSource audioSource;
    private int lastSongIndex = -1;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        Random.InitState(System.DateTime.Now.Millisecond);
        PlayRandomSong();
    }

    void Update()
    {
        // If the audio is not playing, play a new random song
        if (!audioSource.isPlaying)
        {
            PlayRandomSong();
        }
    }

    // Play a random song, avoiding immediate repeats
    private void PlayRandomSong()
    {
        if (radioSongs.Count == 0)
            return;

        int randomIndex;
        do
        {
            randomIndex = Random.Range(0, radioSongs.Count);
        } while (radioSongs.Count > 1 && randomIndex == lastSongIndex);

        audioSource.clip = radioSongs[randomIndex];
        audioSource.Play();
        lastSongIndex = randomIndex;
    }
}
