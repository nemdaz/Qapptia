from dataclasses import dataclass, field


@dataclass
class VectorShape:
    shape_type: str
    shape_id: str
    coords: list[float]
    color: str
    payload: dict = field(default_factory=dict)

    def to_dict(self):
        data = {
            "type": self.shape_type,
            "id": self.shape_id,
            "coords": list(self.coords),
            "color": self.color,
        }
        if self.payload:
            data["payload"] = dict(self.payload)
        return data

    @classmethod
    def from_dict(cls, payload):
        return cls(
            shape_type=payload["type"],
            shape_id=payload["id"],
            coords=list(payload["coords"]),
            color=payload["color"],
            payload=dict(payload.get("payload", {})),
        )
