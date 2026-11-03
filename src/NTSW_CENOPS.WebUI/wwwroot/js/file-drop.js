// CENOPS File Block: drop-to-InputFile bridge. No network calls or global listeners.
// Assigning input.files raises its native change event; Blazor keeps the file handles.
window.cenopsFileDrop = {
  attach(root) {
    if (!root || root.dataset.fileDropAttached === 'true') return;
    if (!root.querySelector('input[type="file"]')) return;
    root.dataset.fileDropAttached = 'true';
    let depth = 0;
    root.addEventListener('dragenter', event => {
      if (!event.dataTransfer?.types?.includes('Files')) return;
      event.preventDefault();
      depth++;
      root.classList.add('is-over');
    });
    root.addEventListener('dragover', event => {
      if (!event.dataTransfer?.types?.includes('Files')) return;
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    });
    root.addEventListener('dragleave', () => {
      if (--depth <= 0) { depth = 0; root.classList.remove('is-over'); }
    });
    root.addEventListener('drop', event => {
      event.preventDefault();
      depth = 0;
      root.classList.remove('is-over');
      if (!event.dataTransfer?.files?.length) return;
      // Always target the newest picker; older ones retain existing file handles.
      const input = root.querySelector('.cenops-file-native-input--active') || root.querySelector('input[type="file"]');
      if (!input || input.disabled) return;
      // DataTransfer creates a normal FileList, supported by modern desktop browsers.
      const transfer = new DataTransfer();
      for (const file of event.dataTransfer.files) transfer.items.add(file);
      input.files = transfer.files;
      input.dispatchEvent(new Event('change', { bubbles: true }));
    });
  }
};
