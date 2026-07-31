from module_capture.infrastructure.capture_settings_repository import capture_settings_repository


class CaptureSettingsService:
    def load(self):
        return capture_settings_repository.load_settings()

    def save(self, settings):
        capture_settings_repository.save_settings(settings)


capture_settings_service = CaptureSettingsService()
