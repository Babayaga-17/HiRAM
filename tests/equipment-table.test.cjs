const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname,
    '../ASI.Basecode.WebApp/wwwroot/js/equipment.js'), 'utf8');

function loadTable({ count = 25, storage = new Map(), blocked = false } = {}) {
    function element(extra = {}) {
        return Object.assign({ value: '', hidden: false, textContent: '',
            listeners: {}, children: [],
            addEventListener(name, callback) { this.listeners[name] = callback; },
            appendChild(child) { this.children.push(child); },
            focus() {},
            set innerHTML(value) { this.children = []; }
        }, extra);
    }
    const elements = {};
    ['equipmentSearch', 'categoryFilter', 'statusFilter', 'equipmentTable',
        'emptyResults', 'resultCount', 'pager', 'detailsDialog',
        'deleteEquipmentDialog', 'clearFilters'].forEach(id => elements[id] = element());
    for (const id of ['categoryFilter', 'statusFilter']) {
        elements[id].tagName = 'SELECT';
        elements[id].value = 'all';
        elements[id].options = ['all', id === 'categoryFilter' ? '1' : 'active']
            .map(value => ({ value }));
    }
    const rows = Array.from({ length: count }, () => element({
        dataset: { name: 'Laptop', code: 'LPT', category: '1', status: 'active' },
        querySelector(selector) { return selector === '.view-equipment' ? element() : null; }
    }));
    const actionForm = element();
    const deleteForm = element();
    const unrelatedForm = element();
    const document = {
        getElementById(id) { return elements[id]; },
        createElement() { return element(); },
        querySelectorAll(selector) {
            if (selector === '#equipmentRows tr') return rows;
            if (selector === '.dialog-close') return [];
            if (selector === "#equipmentRows form[method='post'], #deleteEquipmentDialog form[method='post']")
                return [actionForm, deleteForm];
            if (selector === "form[method='post']") return [actionForm, deleteForm, unrelatedForm];
            throw new Error('Unexpected selector: ' + selector);
        }
    };
    vm.runInNewContext(source, { document, sessionStorage: {
        getItem(key) { if (blocked) throw Error('Storage blocked'); return storage.get(key); },
        removeItem(key) { storage.delete(key); },
        setItem(key, value) { if (blocked) throw Error('Storage blocked'); storage.set(key, value); }
    }});
    return { elements, rows, actionForm, deleteForm, unrelatedForm, storage,
        goTo(page) {
            elements.pager.children.find(button => button.textContent === String(page)).listeners.click();
        }
    };
}

for (const action of ['actionForm', 'deleteForm']) {
    test(`${action} restores page and filters after redirect`, () => {
        const before = loadTable();
        before.elements.equipmentSearch.value = 'Laptop';
        before.elements.equipmentSearch.listeners.input();
        before.elements.categoryFilter.value = '1';
        before.elements.categoryFilter.listeners.change();
        before.elements.statusFilter.value = 'active';
        before.elements.statusFilter.listeners.change();
        before.goTo(3);
        before[action].listeners.submit();
        const after = loadTable({ storage: before.storage });
        assert.equal(after.elements.resultCount.textContent, 'Showing 21–25 of 25 records');
        assert.equal(after.elements.equipmentSearch.value, 'Laptop');
        assert.equal(after.elements.categoryFilter.value, '1');
        assert.equal(after.elements.statusFilter.value, 'active');
        assert.equal(after.storage.size, 0);
    });
}

test('removing the last page falls back to the nearest remaining page', () => {
    const before = loadTable({ count: 21 });
    before.goTo(3);
    before.deleteForm.listeners.submit();
    const after = loadTable({ count: 20, storage: before.storage });
    assert.equal(after.elements.resultCount.textContent, 'Showing 11–20 of 20 records');
});

test('changing a filter resets pagination', () => {
    const table = loadTable();
    table.goTo(3);
    table.elements.equipmentSearch.listeners.input();
    assert.equal(table.elements.resultCount.textContent, 'Showing 1–10 of 25 records');
});

test('unrelated forms do not save table state', () => {
    assert.equal(loadTable().unrelatedForm.listeners.submit, undefined);
});

test('blocked storage does not prevent rendering or submitting actions', () => {
    const table = loadTable({ blocked: true });
    assert.equal(table.elements.resultCount.textContent, 'Showing 1–10 of 25 records');
    assert.doesNotThrow(() => table.actionForm.listeners.submit());
});

test('malformed saved state is ignored', () => {
    const storage = new Map([['hiram.equipment.pendingTableState', '{invalid']]);
    const table = loadTable({ storage });
    assert.equal(table.elements.resultCount.textContent, 'Showing 1–10 of 25 records');
});
