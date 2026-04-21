from module_editor.core.preferences_store import state_store

def get_state_path():
    return state_store.get_state_path()

def load_state():
    return state_store.load().to_dict()

def save_state(state):
    preferences = state_store.load()
    preferences.expanded_folders = list(state.get("expanded_folders", []))
    preferences.last_selected_file = state.get("last_selected_file")
    preferences.active_fav_color = state.get("active_fav_color", preferences.active_fav_color)
    preferences.tool_fav_colors = dict(state.get("tool_fav_colors", preferences.tool_fav_colors))
    state_store.save(preferences)

def update_expanded(folder_path, expanded):
    def mutator(state):
        expanded_folders = set(state.expanded_folders)
        if expanded:
            expanded_folders.add(folder_path)
        else:
            expanded_folders.discard(folder_path)
        state.expanded_folders = sorted(expanded_folders)

    state_store.mutate(mutator)

def set_last_selected(file_path):
    def mutator(state):
        state.last_selected_file = file_path

    state_store.mutate(mutator)

def set_active_color(color_name):
    def mutator(state):
        state.active_fav_color = color_name

    state_store.mutate(mutator)
