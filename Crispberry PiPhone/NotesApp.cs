using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class NotesApp
    {
        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.NotesId,
                DisplayName = "Notes",
                IconGlyph = "N",
                IconBackground = PhoneUi.NotesIcon,
                SortOrder = 30,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.ShowList(); },
                OnClose = () => { _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        private static Session _live;

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _noteId;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public void Relayout()
            {
                if (!string.IsNullOrEmpty(_noteId))
                    return;
                ShowList();
            }

            public void ShowList()
            {
                _noteId = null;
                Clear();
                _host.SetTitle("Notes");
                PhoneUi.MaterialChip(_host.Content, "note_add", "New note", () => Open(PhoneStore.AddNote("New note", string.Empty)), new Vector2(40f, 40f));

                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                if (_host.IsLandscape)
                {
                    var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(380f, 42f);
                    grid.spacing = new Vector2(8f, 6f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 2;
                    grid.padding = new RectOffset(4, 4, 4, 4);
                    grid.childAlignment = TextAnchor.UpperLeft;
                }
                else
                    PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                if (PhoneStore.Notes.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No notes yet.", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                    return;
                }

                for (int i = 0; i < PhoneStore.Notes.Count; i++)
                {
                    NoteItem note = PhoneStore.Notes[i];
                    NoteItem captured = note;
                    string title = string.IsNullOrEmpty(note.Title) ? "Note" : note.Title;
                    var row = new GameObject("Note", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 42f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, title, () => Open(captured), new Vector2(220f, 40f));
                    PhoneUi.MaterialChip(row.transform, "delete", "Delete", () =>
                    {
                        PhoneStore.DeleteNote(captured.Id);
                        ShowList();
                    }, new Vector2(36f, 32f));
                }
            }

            private void Open(NoteItem note)
            {
                _noteId = note != null ? note.Id : null;
                Clear();
                _host.SetTitle("Note");
                PhoneUi.CreateIconChip(_host.Content, "Notes", PhoneIcons.Material("arrow_back"), ShowList, false, new Vector2(36f, 32f));
                TMP_InputField title = PhoneUi.CreateInput(_host.Content, "Title", 80);
                title.text = note.Title ?? string.Empty;
                TMP_InputField body = PhoneUi.CreateMultiline(_host.Content, "Write something", 8000);
                body.text = note.Body ?? string.Empty;
                title.onEndEdit.AddListener(s =>
                {
                    note.Title = s ?? string.Empty;
                    PhoneStore.UpdateNote(note);
                });
                body.onEndEdit.AddListener(s =>
                {
                    note.Body = s ?? string.Empty;
                    PhoneStore.UpdateNote(note);
                });
                PhoneUi.MaterialChip(_host.Content, "delete", "Delete", () =>
                {
                    PhoneStore.DeleteNote(note.Id);
                    ShowList();
                }, new Vector2(36f, 32f));
            }

            private void Clear()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
