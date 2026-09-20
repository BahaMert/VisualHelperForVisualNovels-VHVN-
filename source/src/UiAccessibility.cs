using System;
using System.Linq;
using System.Windows.Forms;
namespace VisualNovelHelper {
public static class UiAccessibility {
    public static void Wire(Form form,Func<bool> enabled,Action<string> speak) { WireChildren(form,form,enabled,speak); }
    static void WireChildren(Form form,Control parent,Func<bool> enabled,Action<string> speak) {
        int index=0;
        foreach(Control control in parent.Controls.Cast<Control>().OrderBy(c=>c.Top).ThenBy(c=>c.Left)) {
            control.TabIndex=index++;
            if(String.IsNullOrWhiteSpace(control.AccessibleName)) control.AccessibleName=control.Text.Replace("&","");
            if(control is Label) control.TabStop=false;
            Action read=()=> { if(speak!=null && enabled() && form.ContainsFocus) {
                string text=control.AccessibleName;
                if(control is Label && text!=control.Text) text+=". "+control.Text;
                var combo=control as ComboBox; if(combo!=null) text+=". "+combo.Text;
                var number=control as NumericUpDown; if(number!=null) text+=". "+number.Value;
                var check=control as CheckBox; if(check!=null) text+=check.Checked?". On":". Off";
                if(!String.IsNullOrWhiteSpace(text)) speak(text);
            }};
            control.Enter+=delegate { read(); }; control.MouseHover+=delegate { read(); };
            if(control.HasChildren && !(control is NumericUpDown) && !(control is ComboBox)) WireChildren(form,control,enabled,speak);
        }
    }
}
}
