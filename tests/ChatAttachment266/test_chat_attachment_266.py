"""2.66 file ownership, parent-scoped delivery, chat integration and overlay regression.
Source assertions supplement (not replace) the .NET/SQL integration tests.
"""
from pathlib import Path
from playwright.sync_api import sync_playwright
import tinycss2
root=Path(__file__).resolve().parents[2]
web=root/'src/NTSW_CENOPS.WebUI'
infra=root/'src/NTSW_CENOPS.Infrastructure'
app=root/'src/NTSW_CENOPS.Application'
read=lambda file:file.read_text(encoding='utf-8')
endpoint=read(web/'Endpoints/FileEndpoints.cs')
policy=read(infra/'Files/FileResourceAccess.cs')
chat=read(web/'Components/Chat/ChatWorkspace.razor')
message=read(infra/'Chat/ChatMessageService.cs')
files=read(web/'Components/Files/AppFileBlock.razor')
preview=read(web/'Components/Files/AppFilePreview.razor')
notes=read(infra/'Notes/NoteService.cs')
model=read(infra/'Persistence/AppDbContext.cs')
migration=read(infra/'Migrations/20261102160000_AddChatMessageAttachments.cs')

assert 'RequireAuthorization()' in endpoint
assert 'if (hasMessage == hasNote) return false;' in endpoint
assert 'return await policy.CanReadAsync(id, messageId, noteId, context.User, ct);' in endpoint
assert endpoint.count('if (!await AuthorizedAsync(id, policy, context, ct)) return Results.NotFound();')==2
assert 'link.FileId == fileId' in policy and 'member.UserId == userId' in policy
assert 'message.DeletedAt == null' in policy
assert 'await notes.FindAsync(noteId.Value, actor, ct)' in policy
assert 'FileReferenceCodec.Parse(block.Content).Contains(fileId)' in policy
assert 'CanDeleteUnattachedAsync' in policy
assert 'file?.OwnerKey' in notes and 'previousFileIds' in notes
assert 'FileReferenceCodec.Parse(x.Content)' in notes
assert 'CreateTable(' in migration and 'ChatMessageAttachments' in migration
assert 'HasKey(x => new { x.MessageId, x.FileId })' in model
assert 'MessageId = msg.Id' in message
assert 'file?.OwnerKey != me.ToString' in message
assert 'BeginTransactionAsync' in message and 'CommitAsync' in message
assert 'forwardedFiles' in message and 'grouped.GetValueOrDefault' in message
assert 'AttachmentIds:' in message
assert 'AppFileBlock @bind-Value="_attachmentDraft" Mode="FileBlockMode.Edit"' in chat
assert 'AppFileBlock Mode="FileBlockMode.View"' in chat
assert 'ChatAttachmentView FileId=' in chat
assert 'IsOpen="_chatPreviewFile is not null"' in chat
assert 'disabled="@(_sending || (string.IsNullOrWhiteSpace(_draft) && AttachedFileCount == 0))"' in chat
assert 'OnPreviewRequest' in files and 'NoteId="NoteId" MessageId="MessageId"' in files
assert 'FileAccess.CanReadAsync' in preview
assert '?messageId=' in read(web/'Components/Chat/ChatAttachmentView.razor')
assert 'NoteId="_preview.Id"' in read(web/'Components/Notes/NoteLibrary.razor')
assert 'NoteId="NoteId"' in read(web/'Components/Notes/NoteContent.razor')
assert 'OwnerKey = null' in read(root/'src/NTSW_CENOPS.FileStreaming/Models/StoredFile.cs')
assert '".mp4", ".webm"' in read(infra/'Files/AppFileGateway.cs')

css=read(web/'wwwroot/css/chat-workspace.css')
assert not [x for x in tinycss2.parse_stylesheet(css,skip_comments=True,skip_whitespace=True) if x.type=='error']
modal=read(web/'Components/Common/AppModal.razor.css')
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page(viewport={'width':1100,'height':850})
    page.set_content(f'''<!doctype html><html lang="fa" dir="rtl"><meta charset="utf-8"><style>{modal}\n{css}</style>
      <body><div class="cenops-chat-backdrop"></div><section class="cenops-chat-window">chat</section>
      <div class="app-modal-backdrop chat-file-modal-overlay"><section class="app-modal-surface">attachments</section></div></body></html>''')
    dialog=page.locator('.app-modal-backdrop')
    assert int(dialog.evaluate('x=>getComputedStyle(x).zIndex'))>int(page.locator('.cenops-chat-window').evaluate('x=>getComputedStyle(x).zIndex'))
    assert dialog.is_visible()
    browser.close()
print('PASS: Chat 2.66 attachment association, parent-scoped authorization, owner guards, overlay placement')
