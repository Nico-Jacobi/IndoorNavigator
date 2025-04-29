
window.indoorControls = {
  elements: {
    up: null,
    down: null,
    level: null
  },
  
  init: function() {
    this.elements.up = document.querySelector('.maplibregl-ctrl-up');
    this.elements.down = document.querySelector('.maplibregl-ctrl-down');
    this.elements.level = document.querySelector('.maplibregl-ctrl-level');

    document.querySelector('.mapboxgl-ctrl-top-left').style.display = 'none';
    document.querySelector('.mapboxgl-ctrl-bottom-right').style.display = 'none';

    return !!(this.elements.up && this.elements.down && this.elements.level);
  },


  // Get current level
  getLevel: function() {
    return this.elements.level ? this.elements.level.textContent : "something went wrong";
  },


  // Navigation actions
  actions: {
    up: function() {
      const upButton = window.indoorControls.elements.up;
      if (upButton) {
        upButton.click();
        return true;
      }
      return false;
    },

    down: function() {
      const downButton = window.indoorControls.elements.down;
      if (downButton) {
        downButton.click();
        return true;
      }
      return false;
    }
  },
};



window.indoorControls.init();
