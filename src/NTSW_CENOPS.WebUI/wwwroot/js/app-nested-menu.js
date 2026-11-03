// Outside-click and Escape behavior is shared by every AppNestedMenu instance.
const bindings = new WeakMap();
export function attach(element, dotnet) {
  detach(element);
  const pointer = event => {
    if (!element.contains(event.target)) dotnet.invokeMethodAsync('Dismiss').catch(console.warn);
  };
  const key = event => {
    if (event.key !== 'Escape' || !element.querySelector('[aria-expanded="true"]')) return;
    event.preventDefault(); dotnet.invokeMethodAsync('Dismiss').catch(console.warn);
    element.querySelector('.app-nested-menu-trigger')?.focus({preventScroll: true});
  };
  const resize = () => place(element);
  document.addEventListener('pointerdown', pointer);
  document.addEventListener('keydown', key);
  window.addEventListener('resize', resize);
  bindings.set(element, () => {
    document.removeEventListener('pointerdown', pointer);
    document.removeEventListener('keydown', key);
    window.removeEventListener('resize', resize);
  });
}
export function detach(element) { bindings.get(element)?.(); bindings.delete(element); }

// Drawer occupies up to 330px. Flip the *entire menu*, not just the drawer,
// when the trigger is close to the viewport's left edge.
export function place(element) {
  const menu = element.querySelector('.app-nested-menu-popover');
  if (!menu) return;
  const anchor = element.getBoundingClientRect();
  const drawerWidth = 330;
  const roomLeft = anchor.right - 10;
  const roomRight = window.innerWidth - anchor.left - 10;
  menu.classList.toggle('left-align', roomLeft < drawerWidth && roomRight > roomLeft);
}
