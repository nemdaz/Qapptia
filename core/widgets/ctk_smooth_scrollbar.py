import customtkinter as ctk

class CTKSmoothScrollbar(ctk.CTkScrollbar):
    """Wrapper de scrollbar que implementa anclaje de puntero (drag offset)."""
    
    def __init__(self, *args, **kwargs):
        self._drag_offset = None
        super().__init__(*args, **kwargs)
        self._canvas.bind("<ButtonRelease-1>", self._on_release, add="+")

    def _on_release(self, event):
        # Reset de estado de arrastre
        self._drag_offset = None

    def _clicked(self, event):
        # Cálculo de valor normalizado según orientación
        is_vertical = str(self._orientation).lower().startswith("v")
        
        if is_vertical:
            click_val = self._reverse_widget_scaling(((event.y - self._border_spacing) / (self._current_height - 2 * self._border_spacing)))
        else:
            click_val = self._reverse_widget_scaling(((event.x - self._border_spacing) / (self._current_width - 2 * self._border_spacing)))
            
        current_len = self._end_value - self._start_value

        # Cálculo de offset relativo al punto de contacto inicial
        if self._drag_offset is None:
            if self._start_value <= click_val <= self._end_value:
                self._drag_offset = click_val - self._start_value
            else:
                self._drag_offset = current_len / 2
        
        # Algoritmo de posicionamiento con preservación de offset y clamping
        new_start = max(0.0, min(click_val - self._drag_offset, 1.0 - current_len))
        
        self._start_value = new_start
        self._end_value = new_start + current_len
        self._draw()
        
        # Sincronización de vista de comando externo
        if self._command is not None:
            self._command('moveto', self._start_value)
