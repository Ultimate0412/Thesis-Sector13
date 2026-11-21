public interface IInteractable
{
    // ข้อความที่จะให้แสดงบน UI เมื่อผู้เล่นมอง (เช่น "กด E เพื่อเปิดกล่อง", "กด E เพื่อหยิบ")
    string GetInteractPrompt();

    // ฟังก์ชันที่จะถูกเรียกเมื่อผู้เล่นกดปุ่ม Interact
    void Interact(PlayerInteractor interactor);
}