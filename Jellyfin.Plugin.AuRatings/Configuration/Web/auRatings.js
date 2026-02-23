var currentView = 'table';
var currentPage = 0;
var itemsPerPage = 50;
var searchTimeout = null;

var AU_RATINGS = [
    { value: 'G', label: 'G' },
    { value: 'PG', label: 'PG' },
    { value: 'M', label: 'M' },
    { value: 'MA 15+', label: 'MA 15+' },
    { value: 'R 18+', label: 'R 18+' },
    { value: 'X 18+', label: 'X 18+' }
];

var PLUGIN_ID = 'b4c7d8e9-2345-6789-bcde-fa0123456789';

function apiFetch(path, options) {
    options = options || {};
    options.headers = options.headers || {};
    options.headers['Authorization'] = 'MediaBrowser Token="' + ApiClient.accessToken() + '"';
    if (options.body && !options.headers['Content-Type']) {
        options.headers['Content-Type'] = 'application/json';
    }
    return fetch(ApiClient.getUrl(path), options).then(function (r) {
        if (!r.ok) throw new Error('API error: ' + r.status);
        return r.json();
    });
}

function loadUsers(view) {
    return apiFetch('AuRatings/Users').then(function (users) {
        var select = view.querySelector('#filterUser');
        while (select.options.length > 1) {
            select.remove(1);
        }
        users.forEach(function (u) {
            var opt = document.createElement('option');
            opt.value = u.Id;
            opt.textContent = u.Username + (u.HasParentalControls ? ' (restricted)' : '');
            select.appendChild(opt);
        });
    });
}

function loadRatings(view) {
    return apiFetch('AuRatings/Ratings').then(function (ratings) {
        var select = view.querySelector('#filterRating');
        while (select.options.length > 1) {
            select.remove(1);
        }
        ratings.forEach(function (r) {
            var opt = document.createElement('option');
            opt.value = r;
            opt.textContent = r;
            select.appendChild(opt);
        });
    });
}

function buildQueryString(view) {
    var params = [];
    params.push('startIndex=' + (currentPage * itemsPerPage));
    params.push('limit=' + itemsPerPage);

    var userId = view.querySelector('#filterUser').value;
    if (userId) params.push('visibleToUser=' + encodeURIComponent(userId));

    var type = view.querySelector('#filterType').value;
    if (type) params.push('type=' + encodeURIComponent(type));

    var rating = view.querySelector('#filterRating').value;
    if (rating) params.push('rating=' + encodeURIComponent(rating));

    var auStatus = view.querySelector('#filterAuStatus').value;
    if (auStatus) params.push('ratingFilter=' + encodeURIComponent(auStatus));

    var sort = view.querySelector('#filterSort').value;
    if (sort) params.push('sortBy=' + encodeURIComponent(sort));

    var search = view.querySelector('#filterSearch').value.trim();
    if (search) params.push('searchTerm=' + encodeURIComponent(search));

    return params.join('&');
}

function loadItems(view) {
    view.querySelector('#loadingIndicator').style.display = 'block';
    view.querySelector('#tableView').style.display = 'none';
    view.querySelector('#cardView').style.display = 'none';

    var qs = buildQueryString(view);
    apiFetch('AuRatings/Items?' + qs).then(function (result) {
        view.querySelector('#loadingIndicator').style.display = 'none';
        renderResult(view, result);
    }).catch(function (err) {
        view.querySelector('#loadingIndicator').style.display = 'none';
        console.error('AU Ratings: failed to load items', err);
    });
}

function renderTable(view, result) {
    view.querySelector('#tableView').style.display = 'block';
    var tbody = view.querySelector('#tableBody');
    tbody.innerHTML = '';

    result.Items.forEach(function (item) {
        var tr = document.createElement('tr');
        tr.style.borderBottom = '1px solid rgba(255,255,255,0.05)';
        tr.style.transition = 'opacity 0.3s, background 0.15s';
        tr.setAttribute('data-item-id', item.Id);

        var displayName = item.Name;
        if (item.SeriesName) {
            displayName = item.SeriesName + ' - ' + item.Name;
        }

        tr.innerHTML =
            '<td style="padding:0.5em;">' + escapeHtml(displayName) + '</td>' +
            '<td style="padding:0.5em;opacity:0.7;font-size:0.9em;">' + escapeHtml(item.Type) + '</td>' +
            '<td style="padding:0.5em;opacity:0.7;font-size:0.9em;">' + escapeHtml(item.OfficialRating || '\u2014') + '</td>' +
            '<td style="padding:0.5em;opacity:0.7;font-size:0.9em;">' + (item.ProductionYear || '\u2014') + '</td>' +
            '<td style="padding:0.5em;"><span class="rating-buttons"></span></td>';

        var btnContainer = tr.querySelector('.rating-buttons');
        renderRatingButtons(view, btnContainer, item);
        tbody.appendChild(tr);
    });
}

function renderCards(view, result) {
    view.querySelector('#cardView').style.display = 'block';
    var container = view.querySelector('#cardContainer');
    container.innerHTML = '';

    result.Items.forEach(function (item) {
        var card = document.createElement('div');
        card.style.cssText = 'background:rgba(255,255,255,0.05);border-radius:8px;overflow:hidden;transition:opacity 0.3s, background 0.15s;';
        card.setAttribute('data-item-id', item.Id);

        var imgUrl = '';
        if (item.HasPrimaryImage) {
            imgUrl = ApiClient.getUrl('Items/' + item.Id + '/Images/Primary', { maxWidth: 300, quality: 90 });
            if (item.PrimaryImageTag) {
                imgUrl += '&tag=' + encodeURIComponent(item.PrimaryImageTag);
            }
        }

        var displayName = item.Name;
        if (item.SeriesName) {
            displayName = item.SeriesName + ' - ' + item.Name;
        }

        var imgHtml = imgUrl
            ? '<img src="' + imgUrl + '" style="width:100%;aspect-ratio:2/3;object-fit:cover;display:block;" loading="lazy" onerror="this.style.display=\'none\'" />'
            : '<div style="width:100%;aspect-ratio:2/3;background:rgba(255,255,255,0.1);display:flex;align-items:center;justify-content:center;"><span class="material-icons" style="font-size:3em;opacity:0.3;">movie</span></div>';

        card.innerHTML =
            imgHtml +
            '<div style="padding:0.5em;">' +
            '<div style="font-size:0.85em;font-weight:500;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;" title="' + escapeHtml(displayName) + '">' + escapeHtml(displayName) + '</div>' +
            '<div style="font-size:0.75em;opacity:0.6;margin-top:0.2em;">' + escapeHtml(item.Type) + (item.OfficialRating ? ' &middot; ' + escapeHtml(item.OfficialRating) : '') + (item.ProductionYear ? ' &middot; ' + item.ProductionYear : '') + '</div>' +
            '<div class="rating-buttons" style="margin-top:0.4em;display:flex;flex-wrap:wrap;gap:0.3em;"></div>' +
            '</div>';

        var btnContainer = card.querySelector('.rating-buttons');
        renderRatingButtons(view, btnContainer, item);
        container.appendChild(card);
    });
}

function renderRatingButtons(view, container, item) {
    AU_RATINGS.forEach(function (ar) {
        var btn = document.createElement('button');
        btn.type = 'button';

        var isActive = item.HasAuRating && item.OfficialRating === ar.value;
        var isSuggested = item.SuggestedAuRating === ar.value;

        btn.style.cssText = 'border:2px solid transparent;border-radius:12px;padding:0.2em 0.6em;font-size:0.8em;cursor:pointer;transition:all 0.2s;';

        if (isActive) {
            btn.style.background = '#00a4dc';
            btn.style.color = '#fff';
            btn.title = 'Click to clear ' + ar.value;
        } else if (isSuggested) {
            btn.style.background = 'rgba(255,255,255,0.1)';
            btn.style.color = 'rgba(255,255,255,0.5)';
            btn.style.borderColor = '#e5a00d';
            btn.title = 'Suggested: ' + ar.value + ' (based on ' + item.OfficialRating + ')';
        } else {
            btn.style.background = 'rgba(255,255,255,0.1)';
            btn.style.color = 'rgba(255,255,255,0.5)';
        }

        btn.textContent = ar.label;

        btn.addEventListener('click', function () {
            if (isActive) {
                clearRating(view, item, container);
            } else {
                setRating(view, item, ar.value, container);
            }
        });

        container.appendChild(btn);
    });
}

function setRating(view, item, rating, container) {
    disableButtons(container);

    apiFetch('AuRatings/SetRating', {
        method: 'POST',
        body: JSON.stringify({ ItemId: item.Id, Rating: rating })
    }).then(function () {
        item.OfficialRating = rating;
        item.HasAuRating = true;
        item.SuggestedAuRating = null;
        rebuildButtons(view, container, item);
        verifyItemInFilters(view, item);
    }).catch(function (err) {
        console.error('AU Ratings: failed to set rating', err);
        enableButtons(container);
    });
}

function clearRating(view, item, container) {
    disableButtons(container);

    apiFetch('AuRatings/ClearRating', {
        method: 'POST',
        body: JSON.stringify({ ItemId: item.Id })
    }).then(function () {
        item.OfficialRating = null;
        item.HasAuRating = false;
        item.SuggestedAuRating = null;
        rebuildButtons(view, container, item);
        verifyItemInFilters(view, item);
    }).catch(function (err) {
        console.error('AU Ratings: failed to clear rating', err);
        enableButtons(container);
    });
}

function renderResult(view, result) {
    if (currentView === 'table') {
        renderTable(view, result);
    } else {
        renderCards(view, result);
    }
    renderPagination(view, result);
}

function verifyItemInFilters(view, item) {
    var qs = buildQueryString(view);
    apiFetch('AuRatings/Items?' + qs).then(function (result) {
        var stillPresent = result.Items.some(function (i) { return i.Id === item.Id; });
        if (!stillPresent) {
            var el = findItemElement(view, item.Id);
            if (el) {
                flashAndRemove(el, function () {
                    renderResult(view, result);
                });
            } else {
                renderResult(view, result);
            }
        }
    });
}

function findItemElement(view, itemId) {
    if (currentView === 'table') {
        return view.querySelector('#tableBody tr[data-item-id="' + itemId + '"]');
    } else {
        return view.querySelector('#cardContainer div[data-item-id="' + itemId + '"]');
    }
}

function flashAndRemove(element, callback) {
    var flashes = 0;
    var maxFlashes = 3;
    var interval = setInterval(function () {
        if (flashes % 2 === 0) {
            element.style.background = 'rgba(0, 164, 220, 0.15)';
        } else {
            element.style.background = '';
        }
        flashes++;
        if (flashes >= maxFlashes * 2) {
            clearInterval(interval);
            element.style.opacity = '0';
            setTimeout(callback, 300);
        }
    }, 150);
}

function disableButtons(container) {
    var buttons = container.querySelectorAll('button');
    for (var i = 0; i < buttons.length; i++) {
        buttons[i].disabled = true;
        buttons[i].style.opacity = '0.5';
    }
}

function enableButtons(container) {
    var buttons = container.querySelectorAll('button');
    for (var i = 0; i < buttons.length; i++) {
        buttons[i].disabled = false;
        buttons[i].style.opacity = '1';
    }
}

function rebuildButtons(view, container, item) {
    container.innerHTML = '';
    renderRatingButtons(view, container, item);
}

function renderPagination(view, result) {
    var total = result.TotalRecordCount;
    var start = result.StartIndex + 1;
    var end = Math.min(result.StartIndex + itemsPerPage, total);

    view.querySelector('#pageInfo').textContent = total > 0
        ? start + '-' + end + ' of ' + total
        : 'No items found';

    view.querySelector('#btnPrev').disabled = currentPage === 0;
    view.querySelector('#btnNext').disabled = end >= total;
}

function setView(view, mode) {
    currentView = mode;
    view.querySelector('#btnTableView').style.opacity = mode === 'table' ? '1' : '0.5';
    view.querySelector('#btnCardView').style.opacity = mode === 'cards' ? '1' : '0.5';
    loadItems(view);
}

function resetAndLoad(view) {
    currentPage = 0;
    loadItems(view);
}

function escapeHtml(text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.appendChild(document.createTextNode(text));
    return div.innerHTML;
}

export default function (view) {
    view.addEventListener('viewshow', function () {
        ApiClient.getPluginConfiguration(PLUGIN_ID).then(function (config) {
            itemsPerPage = config.ItemsPerPage || 50;
            if (config.DefaultView === 'Cards') {
                currentView = 'cards';
            }
            setView(view, currentView);
        }).catch(function () {
            setView(view, currentView);
        });

        loadRatings(view);
        loadUsers(view);
    });

    view.querySelector('#btnTableView').addEventListener('click', function () { setView(view, 'table'); });
    view.querySelector('#btnCardView').addEventListener('click', function () { setView(view, 'cards'); });

    view.querySelector('#filterUser').addEventListener('change', function () { resetAndLoad(view); });
    view.querySelector('#filterType').addEventListener('change', function () { resetAndLoad(view); });
    view.querySelector('#filterRating').addEventListener('change', function () { resetAndLoad(view); });
    view.querySelector('#filterAuStatus').addEventListener('change', function () { resetAndLoad(view); });
    view.querySelector('#filterSort').addEventListener('change', function () { resetAndLoad(view); });

    view.querySelector('#filterSearch').addEventListener('input', function () {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(function () { resetAndLoad(view); }, 400);
    });

    view.querySelector('#btnPrev').addEventListener('click', function () {
        if (currentPage > 0) {
            currentPage--;
            loadItems(view);
        }
    });
    view.querySelector('#btnNext').addEventListener('click', function () {
        currentPage++;
        loadItems(view);
    });
}
