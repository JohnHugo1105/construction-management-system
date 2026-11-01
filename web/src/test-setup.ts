// Runs once before the specs. jsdom does not implement the native <dialog> methods, so these stand in for them
// closely enough for behaviour tests (open attribute, close event); real browsers use their own implementation.
const dialogPrototype = globalThis.HTMLDialogElement?.prototype;

if (dialogPrototype) {
  dialogPrototype.showModal = function showModal(this: HTMLDialogElement): void {
    this.setAttribute('open', '');
  };
  dialogPrototype.show = function show(this: HTMLDialogElement): void {
    this.setAttribute('open', '');
  };
  dialogPrototype.close = function close(this: HTMLDialogElement): void {
    if (!this.hasAttribute('open')) return;
    this.removeAttribute('open');
    this.dispatchEvent(new Event('close'));
  };
}
