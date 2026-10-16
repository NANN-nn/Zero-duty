using System;
using UnityEngine;

/// <summary>Optional collectibles, snacks and readable props; no task rewards.</summary>
[DisallowMultipleComponent]
public class OptionalPropInteraction : MonoBehaviour, IPlayerInteractable
{
    public enum PropKind { Robot, Food, Note }
    public enum NotePaperStyle { Auto, Book, Sticky, Record }
    [SerializeField] private PropKind kind;
    [SerializeField] private bool drink;
    [SerializeField] private NotePaperStyle notePaperStyle = NotePaperStyle.Auto;
    [SerializeField] private string noteTitle = "";
    [SerializeField, TextArea(8, 24)] private string noteBody = "";
    [SerializeField] private AudioClip consumptionAudio;
    private bool used;
    public string InteractionPrompt => "";
    public PropKind Kind => kind;
    public bool IsDrink => drink;
    public string NoteTitle => noteTitle;
    public string NoteBody => noteBody;
    public NotePaperStyle NoteStyle
    {
        get
        {
            if (notePaperStyle != NotePaperStyle.Auto) return notePaperStyle;
            MeshFilter mesh = GetComponent<MeshFilter>();
            string model = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.name : string.Empty;
            if (name.StartsWith("ShiftLog_", StringComparison.Ordinal)
                || model.StartsWith("ShiftLog_", StringComparison.Ordinal)) return NotePaperStyle.Book;
            if (name.StartsWith("FoldedPersonalNote", StringComparison.Ordinal)
                || model.StartsWith("FoldedPersonalNote", StringComparison.Ordinal)) return NotePaperStyle.Sticky;
            return NotePaperStyle.Record;
        }
    }
    public AudioClip ConsumptionAudio => consumptionAudio;
    private bool IsDecoration
    {
        get
        {
            MeshFilter mesh = GetComponent<MeshFilter>();
            return name.StartsWith("StickyNotes_Stack", StringComparison.Ordinal)
                || mesh != null && mesh.sharedMesh != null
                    && mesh.sharedMesh.name.StartsWith("StickyNotes_Stack", StringComparison.Ordinal);
        }
    }

    public void Configure(PropKind value, bool isDrink = false)
    {
        kind = value;
        drink = isDrink;
        if (kind == PropKind.Note) { ConfigurePlacedNote(); EnsureHandbookCue(); }
    }
    public void EnsureNoteContent()
    {
        if (kind == PropKind.Note) ConfigurePlacedNote();
    }
    public void CancelConsumption() { if (kind == PropKind.Food) used = false; }
    private void Awake()
    {
        // Older placed prefabs still serialize this script. Keep their visible
        // mesh and physical collider, but never offer a reading interaction.
        if (IsDecoration) { enabled = false; return; }
        RuntimeColliderUtility.EnsureBoxCollider(gameObject);
        if (kind == PropKind.Note) { ConfigurePlacedNote(); EnsureHandbookCue(); }
    }

    private void EnsureHandbookCue()
    {
        if (name == "ShiftLog_Open1" && GetComponent<HandbookReadCue>() == null)
            gameObject.AddComponent<HandbookReadCue>();
    }

    // Only the numbered scene props receive story text. The unnumbered
    // StickyNotes_Stack is a decorative prop, never a readable note.
    private void ConfigurePlacedNote()
    {
        switch (name)
        {
            case "ShiftLog_Open1":
                notePaperStyle = NotePaperStyle.Book;
                noteTitle = "NIGHT SHIFT HANDBOOK";
                noteBody =
                    "YOUR DUTIES\n\n"
                    + "Monitor the facility and investigate any changes during your shift. The photographs on the wall show each room in its normal condition.\n\n"
                    + "SHIFT PROCEDURE\n\n"
                    + "A brief alarm will sound whenever a new incident occurs. Check the camera feeds, attend the affected area, then return to the monitors once the problem is resolved.\n\n"
                    + "Do not leave abnormal objects unattended. Take them to the disposal unit in the laboratory.\n\n"
                    + "CONTROLS\n\n"
                    + "WASD — Move\nMouse — Look around\nE — Interact or pick up\n"
                    + "Q — Put down held items\nLeft mouse — Use held equipment\n"
                    + "ESC — Close notes or camera views; otherwise open the pause menu"
                    + "\fFOR THE NEXT SHIFT\n\n"
                    + "The gas and power controls are right beside the monitors. Same panel for both.\n\n"
                    + "Hold the pressure button down. A quick tap won't do much. Holding raises the pressure; letting go lowers it. Keep the needle in the green section for a moment. One light isn't enough—you need all three.\n\n"
                    + "For the breakers, turn the three connectors at the top until the circuit connects. The lights will come on one after another. Remember the colors, then pull the levers below in that order. Replay it if you missed it. Don't guess.\n\n"
                    + "The gun and scanner are on the equipment rack. Use the gun for whatever shows up in the shooting room. Take the scanner to the specimen room. Looking at the containers won't tell you much.\n\n"
                    + "Scan the jars in the cabinet and on the table. Last time I only checked the cabinet. Found another one on the table after I'd cleaned up.\n\n"
                    + "Come back and check the monitors when you're done. I know it feels like you've only just sat down. Just because something's gone doesn't mean the night's over.\n\n"
                    + "— M.";
                break;
            case "InspectionClipboard2":
                notePaperStyle = NotePaperStyle.Record;
                noteTitle = "MONITORING LOG";
                noteBody =
                    "02:14 — Someone was standing by the shooting-room door on Camera 03. I went over to check. The room was empty.\n\n"
                    + "02:19 — The same person appeared in the corridor on Camera 01. I checked the recording. None of the doors had opened.\n\n"
                    + "I put it down as a camera fault.\n"
                    + "I haven't signed the report yet.";
                break;
            case "FoldedPersonalNote3":
                notePaperStyle = NotePaperStyle.Sticky;
                noteTitle = "THE PHOTOS";
                noteBody =
                    "I took these when I first started working nights. I kept forgetting what was in each room.\n\n"
                    + "A few times, something on the monitors looked out of place. Comparing it with the photos was how I knew I wasn't remembering it wrong.\n\n"
                    + "— M.";
                break;
            case "FoldedPersonalNote4":
                notePaperStyle = NotePaperStyle.Sticky;
                noteTitle = "AFTER THE LEAK";
                noteBody =
                    "The hissing stopped once the pressure settled.\n\n"
                    + "I sat back down at the monitors. Behind me, someone let out a long breath.\n\n"
                    + "The next shift wasn't due for another three hours.\n\n"
                    + "— M.";
                break;
            case "InspectionClipboard5":
                notePaperStyle = NotePaperStyle.Record;
                noteTitle = "CORRIDOR INCIDENT";
                noteBody =
                    "01:42 — Found a framed picture on the corridor floor.\n\n"
                    + "01:49 — Destroyed it in the laboratory disposal unit.\n\n"
                    + "01:53 — It's back.\n\n"
                    + "The same corner is missing. That's where I dropped it.\n\n"
                    + "I watched that frame go through the machine. The pieces are still in there.\n\n"
                    + "I'm not going back out there.";
                break;
            case "FoldedPersonalNote6":
                notePaperStyle = NotePaperStyle.Sticky;
                noteTitle = "FOOTSTEPS";
                noteBody =
                    "I reported footsteps in the corridor. The cameras showed nobody there.\n\n"
                    + "They wrote \"pipe noise\" on the report and sent it back.\n\n"
                    + "Pipes don't stop when I stop walking.";
                break;
            case "FoldedPersonalNote7":
                notePaperStyle = NotePaperStyle.Sticky;
                noteTitle = "AFTER THE FIRST SHOT";
                noteBody =
                    "It disappeared when I fired. I thought that was it, so I lowered the gun.\n\n"
                    + "Then I caught its reflection in the glass.\n\n"
                    + "It was standing behind me.\n\n"
                    + "I hadn't heard a single footstep.\n\n"
                    + "— A.";
                break;
            case "InspectionClipboard8":
                notePaperStyle = NotePaperStyle.Record;
                noteTitle = "SPECIMEN ROOM LOG";
                noteBody =
                    "03:08 — The container in the cabinet looked intact. The scanner gave a positive reading.\n\n"
                    + "03:12 — A second reading came from the container on the worktable.\n\n"
                    + "Both labels carry the same sample number and collection date.\n\n"
                    + "I checked the inventory twice. We only received one.";
                break;
            case "FoldedPersonalNote9":
                notePaperStyle = NotePaperStyle.Sticky;
                noteTitle = "LITTLE HELPERS";
                noteBody =
                    "Bought five of these little robots for the desk. Nobody else seemed to like them.\n\n"
                    + "Now they're scattered all over the place. No idea who split them up.\n\n"
                    + "I still think they look better together.\n\n"
                    + "— M.";
                break;
            case "FoldedPersonalNote10":
                notePaperStyle = NotePaperStyle.Sticky;
                noteTitle = "DISPOSAL UNIT";
                noteBody =
                    "This is the machine from the handbook. I put the thing I found in the corridor through it last night.\n\n"
                    + "It took the whole frame. Nothing left big enough to recognize.\n\n"
                    + "Loud as hell, but I felt better once it was gone.\n\n"
                    + "— M.";
                break;
        }
    }
    public bool CanInteract(PlayerInteractor interactor)
        => isActiveAndEnabled && !IsDecoration && !used && interactor != null && interactor.InputEnabled
           && !OptionalPropActivities.Busy && ReferencePictureView.ActiveView == null
           && MonitorViewManager.ActiveSession == null;
    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        if (kind == PropKind.Note) OptionalPropActivities.Instance.Read(this, interactor);
        else
        {
            used = true;
            if (kind == PropKind.Robot) OptionalPropActivities.Instance.Collect(this);
            else OptionalPropActivities.Instance.Consume(this, interactor);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConnectExistingProps()
    {
        foreach (MeshFilter mesh in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<OptionalPropInteraction>()) continue;
            string name = mesh.sharedMesh.name;
            if (!TryIdentify(name, out PropKind type, out bool drinking)) continue;
            mesh.gameObject.AddComponent<OptionalPropInteraction>().Configure(type, drinking);
        }
    }
    public static bool TryIdentify(string name, out PropKind type, out bool drinking)
    {
        type = PropKind.Note; drinking = false;
        if (name.StartsWith("MaintenanceRobot_Mascot", StringComparison.Ordinal)) { type = PropKind.Robot; return true; }
        if (name.StartsWith("CoffeeCan", StringComparison.Ordinal)) { type = PropKind.Food; drinking = true; return true; }
        if (name.StartsWith("OatBiscuit", StringComparison.Ordinal) || name.StartsWith("RationPack", StringComparison.Ordinal)) { type = PropKind.Food; return true; }
        return name.StartsWith("ShiftLog_", StringComparison.Ordinal)
            || name.StartsWith("InspectionClipboard", StringComparison.Ordinal)
            || name.StartsWith("FoldedPersonalNote", StringComparison.Ordinal);
    }
}
