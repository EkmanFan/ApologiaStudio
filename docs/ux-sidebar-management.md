# Sidebar management contract

## Scope

UX-03 exposes the write interactions supported by the UX-02 organization
model. All commands are scoped to the current user and persist through the
application and PostgreSQL layers.

## Projects

- A project can be created and renamed.
- Project names are required, limited to 120 characters, and unique per user
  without regard to case at the application boundary.
- Projects can be reordered by drag-and-drop or by the keyboard-accessible
  move-up and move-down actions.
- Deleting a project requires confirmation.
- Deleting a project returns all of its active and recoverable conversations to
  `Chats`. It never deletes a conversation.
- A deleted project's pinned shortcut is removed.

## Conversations

- A conversation can be renamed, pinned, unpinned, reordered, or moved between
  `Chats` and a project.
- A conversation belongs to at most one project.
- Moving and ordering are one transactional command: the source and destination
  containers are both normalized to contiguous zero-based positions.
- Deleting a conversation requires confirmation and is recoverable.

## Recoverable deletion

- Conversation deletion sets `deleted_at`; messages and ownership are retained.
- A deleted conversation is excluded from active navigation and cannot be used
  for chat or pin operations.
- Any pinned shortcut to the conversation is removed at deletion time.
- Deleted conversations appear in `Trash` and can be restored.
- Restoration appends the conversation to its surviving container. If its
  former project was deleted, the database relationship places it in `Chats`.
- Permanent deletion and retention-period cleanup are outside UX-03.

## Pinned shortcuts and ordering

- Pinning remains a shortcut operation and never moves the target.
- Projects, conversations within each container, and pinned shortcuts support
  native drag-and-drop.
- Equivalent move-up and move-down actions remain available for keyboard and
  touch users.
- Reorder commands require the complete owned identifier set exactly once,
  preventing partial or cross-user reordering.

## Failure behavior

- Commands reject missing or foreign targets before mutation.
- Invalid destinations and positions are rejected.
- Each interaction is saved through one unit of work.
- The sidebar is reloaded after each successful command; failures preserve the
  current view and surface an error.
