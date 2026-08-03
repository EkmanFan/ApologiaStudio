# Sidebar organization contract

## Scope

UX-02 introduced the persistent model and read projection for the `Pinned`,
`Projects`, and `Chats` sections. UX-03 adds the write commands and controls
defined in [the sidebar management contract](ux-sidebar-management.md).

## Navigation semantics

The sidebar renders sections in this order:

1. `Library`;
2. `Pinned`, when at least one shortcut exists;
3. `Projects`, including its creation control;
4. `Chats`.

`Trash` follows `Chats` only when at least one recoverable conversation exists.

`Chats` contains only conversations that do not belong to a project. A
conversation belongs to at most one project.

`Pinned` contains shortcuts. Pinning a project or conversation never moves the
target and never creates a copy. A pinned conversation therefore remains
visible in either its project or `Chats`.

## Ownership and integrity

- Projects, conversations, and pins are scoped to one owner.
- A conversation can only move to a project with the same owner.
- A pin targets exactly one conversation or one project.
- A user can pin a given conversation or project at most once.
- Deleting a pinned target removes its shortcut.
- Deleting a project returns its conversations to `Chats`; it does not delete
  those conversations.

Ownership is checked by the domain and application layers. PostgreSQL foreign
keys, check constraints, and unique indexes enforce structural integrity.

## Manual ordering

Projects, conversations, and pinned shortcuts each store a non-negative
`sort_order` value. Ordering is evaluated within the relevant container:

- projects within `Projects`;
- unassigned conversations within `Chats`;
- assigned conversations within their project;
- shortcuts within `Pinned`.

Creation dates provide deterministic fallback ordering when multiple records
have the same `sort_order`, including records created before UX-02.

## Write interactions

UX-03 implements project creation, renaming and deletion, pinning, moving,
keyboard-accessible ordering, drag-and-drop ordering, recoverable conversation
deletion, and restoration.
