/**
 * ShortcutManager provides a simple way to register and manage keyboard shortcuts.
 */
const ShortcutManager = (function() {
    const shortcuts = new Map();

    /**
     * Handles the keydown event and executes the registered callback for the key.
     * @param {KeyboardEvent} event 
     */
    function handleKeyDown(event) {

        const callback = shortcuts.get(event.key);
        if (callback && typeof callback === 'function') {
            event.preventDefault();
            callback();
        }
    }

    // Initialize the event listener
    window.addEventListener('keydown', handleKeyDown);

    return {
        /**
         * Registers a shortcut for a specific key.
         * @param {string} key - The key name (e.g., 'ArrowLeft', 'Enter', 'a')
         * @param {Function} callback - The function to execute when the key is pressed
         */
        register: function(key, callback) {
            shortcuts.set(key, callback);
        },

        /**
         * Unregisters a shortcut for a specific key.
         * @param {string} key 
         */
        unregister: function(key) {
            shortcuts.delete(key);
        }
    };
})();

// Export to window object for global access
window.ShortcutManager = ShortcutManager;
