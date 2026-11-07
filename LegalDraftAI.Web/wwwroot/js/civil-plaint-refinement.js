(() => {
    const form = document.getElementById("civil-plaint-form");
    if (!form) return;

    const copy = {
        en: {
            refine: "Improve with AI",
            refining: "Improving text…",
            suggestion: "AI suggestion — review before applying",
            apply: "Apply suggestion",
            discard: "Discard",
            empty: "Enter text before requesting a refinement.",
            failed: "Could not refine this text.",
            changed: "This field changed while the suggestion was being prepared. Review the suggestion carefully or try again.",
            applied: "Suggestion applied. Review the field before continuing.",
            networkError: "Could not reach the refinement service. Check your connection and try again.",
            disclaimer: "Selected text is sent to the configured AI service. Review carefully; AI may alter meaning."
        },
        hi: {
            refine: "AI से सुधारें",
            refining: "पाठ सुधारा जा रहा है…",
            suggestion: "AI सुझाव — लागू करने से पहले समीक्षा करें",
            apply: "सुझाव लागू करें",
            discard: "हटाएँ",
            empty: "सुधार का अनुरोध करने से पहले पाठ दर्ज करें।",
            failed: "इस पाठ में सुधार नहीं हो सका।",
            changed: "सुझाव तैयार होते समय यह फ़ील्ड बदल गया। सुझाव की सावधानीपूर्वक समीक्षा करें या फिर से प्रयास करें।",
            applied: "सुझाव लागू किया गया। आगे बढ़ने से पहले फ़ील्ड की समीक्षा करें।",
            networkError: "सुधार सेवा से संपर्क नहीं हो सका। कनेक्शन जाँचकर फिर प्रयास करें।",
            disclaimer: "चुना गया पाठ कॉन्फ़िगर की गई AI सेवा को भेजा जाएगा। सावधानी से जाँचें; AI अर्थ बदल सकता है।"
        }
    };

    const fields = Array.from(form.querySelectorAll("[data-ai-refine-field]"));
    const getWords = () => document.documentElement.lang === "hi" ? copy.hi : copy.en;

    fields.forEach((field) => {
        const container = document.createElement("div");
        container.className = "ai-refinement";

        const actions = document.createElement("div");
        actions.className = "ai-refinement-actions";

        const disclaimer = document.createElement("p");
        disclaimer.className = "ai-refinement-disclaimer";

        const refineButton = document.createElement("button");
        refineButton.type = "button";
        refineButton.className = "ai-refinement-button";
        refineButton.dataset.refineAction = "refine";
        actions.append(refineButton);

        const status = document.createElement("span");
        status.className = "ai-refinement-status";
        status.setAttribute("role", "status");
        status.setAttribute("aria-live", "polite");
        actions.append(status);

        const suggestion = document.createElement("div");
        suggestion.className = "ai-refinement-suggestion";
        suggestion.hidden = true;

        const suggestionLabel = document.createElement("label");
        const suggestionId = `${field.id}-ai-suggestion`;
        suggestionLabel.htmlFor = suggestionId;

        const suggestionText = document.createElement("textarea");
        suggestionText.id = suggestionId;
        suggestionText.className = "form-control";
        suggestionText.readOnly = true;
        suggestionText.rows = Math.max(3, Math.min(8, field.rows || 4));

        const suggestionActions = document.createElement("div");
        suggestionActions.className = "ai-refinement-actions";

        const applyButton = document.createElement("button");
        applyButton.type = "button";
        applyButton.className = "secondary-button";
        applyButton.dataset.refineAction = "apply";

        const discardButton = document.createElement("button");
        discardButton.type = "button";
        discardButton.className = "text-button";
        discardButton.dataset.refineAction = "discard";

        suggestionActions.append(applyButton, discardButton);
        suggestion.append(suggestionLabel, suggestionText, suggestionActions);
        container.append(disclaimer, actions, suggestion);
        field.insertAdjacentElement("afterend", container);

        const updateLabels = () => {
            const words = getWords();
            disclaimer.textContent = words.disclaimer;
            refineButton.textContent = words.refine;
            suggestionLabel.textContent = words.suggestion;
            applyButton.textContent = words.apply;
            discardButton.textContent = words.discard;
        };
        updateLabels();

        field.addEventListener("input", () => {
            suggestion.hidden = true;
            suggestionText.value = "";
            status.textContent = "";
            refineButton.disabled = !field.value.trim();
        });

        refineButton.disabled = !field.value.trim();
        refineButton.addEventListener("click", async () => {
            const originalText = field.value.trim();
            if (!originalText) {
                status.textContent = getWords().empty;
                return;
            }

            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token) {
                status.textContent = getWords().failed;
                return;
            }

            const words = getWords();
            refineButton.disabled = true;
            refineButton.textContent = words.refining;
            status.textContent = "";
            suggestion.hidden = true;

            const url = new URL(window.location.href);
            url.searchParams.set("handler", "RefineText");
            let response;
            try {
                response = await fetch(url, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token
                    },
                    body: JSON.stringify({
                        field: field.dataset.aiRefineField,
                        text: originalText
                    })
                });
            } catch {
                status.textContent = getWords().networkError;
                refineButton.disabled = !field.value.trim();
                refineButton.textContent = getWords().refine;
                return;
            }

            let result;
            try {
                result = await response.json();
            } catch {
                status.textContent = getWords().failed;
                refineButton.disabled = !field.value.trim();
                refineButton.textContent = getWords().refine;
                return;
            }
            if (!response.ok) {
                status.textContent = result.error || getWords().failed;
                refineButton.disabled = !field.value.trim();
                refineButton.textContent = getWords().refine;
                return;
            }

            if (field.value.trim() !== originalText) {
                suggestionText.value = result.refinedText;
                suggestion.hidden = false;
                status.textContent = getWords().changed;
                refineButton.disabled = !field.value.trim();
                refineButton.textContent = getWords().refine;
                return;
            }

            suggestionText.value = result.refinedText;
            suggestion.hidden = false;
            refineButton.disabled = !field.value.trim();
            refineButton.textContent = getWords().refine;
        });

        applyButton.addEventListener("click", () => {
            field.value = suggestionText.value;
            field.dispatchEvent(new Event("input", { bubbles: true }));
            field.dispatchEvent(new Event("change", { bubbles: true }));
            suggestion.hidden = true;
            status.textContent = getWords().applied;
        });

        discardButton.addEventListener("click", () => {
            suggestion.hidden = true;
            suggestionText.value = "";
        });

        document.querySelectorAll("[data-page-language]").forEach((button) => {
            button.addEventListener("click", updateLabels);
        });
    });
})();
