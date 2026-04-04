from module_editor.core.vector_store import vector_store

def get_json_path(image_path):
    return vector_store.get_json_path(image_path)

def load_vectors(image_path):
    return vector_store.load(image_path)

def save_vectors(image_path, vectors):
    vector_store.save(image_path, vectors)
